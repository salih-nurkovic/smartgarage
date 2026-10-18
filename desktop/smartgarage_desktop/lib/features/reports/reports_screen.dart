import 'dart:math' as math;
import 'dart:typed_data';

import 'package:file_selector/file_selector.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:printing/printing.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/report.dart';

typedef _Reports = (RevenueReport revenue, AppointmentReport appointments);

final _reportsProvider = FutureProvider.autoDispose.family<_Reports, ReportRange>((ref, range) {
  final reports = ref.watch(reportRepositoryProvider);
  return (reports.revenue(range), reports.appointments(range)).wait;
});

/// The periods offered in the dropdown. Days are the garage's (UTC) days, like every other
/// date in the system.
enum _Preset {
  thisMonth('Ovaj mjesec'),
  lastMonth('Prošli mjesec'),
  lastThreeMonths('Zadnja 3 mjeseca'),
  lastSixMonths('Zadnjih 6 mjeseci'),
  thisYear('Ova godina'),
  custom('Prilagođeni period…');

  const _Preset(this.label);

  final String label;

  ReportRange? range(DateTime today) => switch (this) {
    thisMonth => ReportRange(DateTime.utc(today.year, today.month), today),
    lastMonth => ReportRange(DateTime.utc(today.year, today.month - 1), DateTime.utc(today.year, today.month, 0)),
    lastThreeMonths => ReportRange(DateTime.utc(today.year, today.month - 2), today),
    lastSixMonths => ReportRange(DateTime.utc(today.year, today.month - 5), today),
    thisYear => ReportRange(DateTime.utc(today.year), today),
    custom => null,
  };
}

enum _Tab { revenue, appointments }

class ReportsScreen extends ConsumerStatefulWidget {
  const ReportsScreen({super.key});

  @override
  ConsumerState<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends ConsumerState<ReportsScreen> {
  late final DateTime _today;
  var _preset = _Preset.lastSixMonths;
  late ReportRange _range;
  var _tab = _Tab.revenue;
  var _busy = false;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now().toUtc();
    _today = DateTime.utc(now.year, now.month, now.day);
    _range = _preset.range(_today)!;
  }

  Future<void> _choose(_Preset preset) async {
    var range = preset.range(_today);
    if (range == null) {
      final picked = await showDateRangePicker(
        context: context,
        // The API refuses ranges longer than two years.
        firstDate: DateTime.utc(_today.year - 2, _today.month, _today.day + 1),
        lastDate: _today.add(const Duration(days: 60)),
        initialDateRange: DateTimeRange(start: _range.from, end: _range.to),
        helpText: 'Izaberite period izvještaja',
        saveText: 'Primijeni',
      );
      if (picked == null) {
        return;
      }
      range = ReportRange(
        DateTime.utc(picked.start.year, picked.start.month, picked.start.day),
        DateTime.utc(picked.end.year, picked.end.month, picked.end.day),
      );
    }
    setState(() {
      _preset = preset;
      _range = range!;
    });
  }

  Future<Uint8List?> _pdf(ReportKind kind) async {
    setState(() => _busy = true);
    try {
      return await ref.read(reportRepositoryProvider).pdf(kind, _range);
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
      return null;
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _download(ReportKind kind) async {
    final location = await getSaveLocation(
      suggestedName: kind.fileName(_range),
      acceptedTypeGroups: const [
        XTypeGroup(label: 'PDF', extensions: ['pdf']),
      ],
    );
    if (location == null) {
      return;
    }
    final bytes = await _pdf(kind);
    if (bytes == null) {
      return;
    }
    await XFile.fromData(bytes, mimeType: 'application/pdf').saveTo(location.path);
    if (mounted) {
      showMessage(context, '${kind.label} je sačuvan.');
    }
  }

  Future<void> _print(ReportKind kind) async {
    final bytes = await _pdf(kind);
    if (bytes == null) {
      return;
    }
    await Printing.layoutPdf(onLayout: (_) async => bytes, name: kind.fileName(_range));
  }

  @override
  Widget build(BuildContext context) {
    final reports = ref.watch(_reportsProvider(_range));

    return ScreenScaffold(
      title: 'Izvještaji',
      subtitle: 'Prihodi i rezervacije za izabrani period',
      actions: [
        SizedBox(
          width: 220,
          child: DropdownButtonFormField<_Preset>(
            initialValue: _preset,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Period', isDense: true),
            items: [for (final preset in _Preset.values) DropdownMenuItem(value: preset, child: Text(preset.label))],
            onChanged: _busy ? null : (preset) => _choose(preset!),
            // A custom period shows the dates it stands for.
            selectedItemBuilder: (_) => [
              for (final preset in _Preset.values)
                Text(preset == _Preset.custom ? '${Fmt.date(_range.from)} – ${Fmt.date(_range.to)}' : preset.label),
            ],
          ),
        ),
        _ReportMenu(
          label: 'Izvezi izvještaj',
          icon: Icons.download_outlined,
          filled: true,
          busy: _busy,
          onSelected: _download,
        ),
        _ReportMenu(label: 'Ispiši', icon: Icons.print_outlined, busy: _busy, onSelected: _print),
      ],
      body: AsyncBody<_Reports>(
        value: reports,
        onRetry: () => ref.invalidate(_reportsProvider(_range)),
        builder: (data) => _ReportBody(
          revenue: data.$1,
          appointments: data.$2,
          tab: _tab,
          onTab: (tab) => setState(() => _tab = tab),
        ),
      ),
    );
  }
}

/// A button that opens a menu with the two reports, so one action covers both.
class _ReportMenu extends StatelessWidget {
  const _ReportMenu({
    required this.label,
    required this.icon,
    required this.busy,
    required this.onSelected,
    this.filled = false,
  });

  final String label;
  final IconData icon;
  final bool busy;
  final bool filled;
  final void Function(ReportKind kind) onSelected;

  @override
  Widget build(BuildContext context) => MenuAnchor(
    menuChildren: [
      for (final kind in ReportKind.values)
        MenuItemButton(
          leadingIcon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
          onPressed: () => onSelected(kind),
          child: Text('${kind.label} (PDF)'),
        ),
    ],
    builder: (context, controller, _) {
      void toggle() => controller.isOpen ? controller.close() : controller.open();
      final iconWidget = busy
          ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
          : Icon(icon, size: 18);
      return filled
          ? FilledButton.icon(onPressed: busy ? null : toggle, icon: iconWidget, label: Text(label))
          : OutlinedButton.icon(onPressed: busy ? null : toggle, icon: iconWidget, label: Text(label));
    },
  );
}

class _ReportBody extends ConsumerWidget {
  const _ReportBody({required this.revenue, required this.appointments, required this.tab, required this.onTab});

  final RevenueReport revenue;
  final AppointmentReport appointments;
  final _Tab tab;
  final ValueChanged<_Tab> onTab;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final money = ref.watch(moneyProvider);
    final number = NumberFormat.decimalPattern(appLocale);
    final rating = appointments.averageRating;
    const meaning = 'U odnosu na prethodni period iste dužine';

    return ListView(
      padding: const EdgeInsets.only(bottom: 28),
      children: [
        Row(
          children: [
            Expanded(
              child: StatCard(
                label: 'Ukupan prihod',
                value: money(revenue.netRevenue.value),
                icon: Icons.payments_outlined,
                changePercent: revenue.netRevenue.changePercent,
                changeMeaning: meaning,
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: StatCard(
                label: 'Ukupno rezervacija',
                value: number.format(appointments.total.value),
                icon: Icons.event_note_outlined,
                color: AppColors.success,
                changePercent: appointments.total.changePercent,
                changeMeaning: meaning,
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: StatCard(
                label: 'Prosječna cijena',
                value: money(appointments.averagePrice.value),
                icon: Icons.sell_outlined,
                color: AppColors.warning,
                changePercent: appointments.averagePrice.changePercent,
                changeMeaning: meaning,
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Tooltip(
                message: rating == null
                    ? 'U ovom periodu nema recenzija.'
                    : 'Prosječna ocjena ${rating.value.toStringAsFixed(2)} od 5',
                child: StatCard(
                  label: 'Stopa zadovoljstva',
                  // The average rating as a share of the best possible one.
                  value: rating == null ? '–' : '${(rating.value / 5 * 100).toStringAsFixed(0)}%',
                  icon: Icons.star_outline,
                  color: AppColors.violet,
                  changePercent: rating?.changePercent,
                  changeMeaning: meaning,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 20),
        SizedBox(
          height: 420,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                flex: 2,
                child: _PeriodChart(revenue: revenue.byPeriod, booked: appointments.byPeriod, money: money),
              ),
              const SizedBox(width: 16),
              Expanded(child: _DistributionChart(shares: appointments.byCategory)),
            ],
          ),
        ),
        const SizedBox(height: 20),
        Align(
          alignment: Alignment.centerLeft,
          child: SegmentedButton<_Tab>(
            segments: const [
              ButtonSegment(value: _Tab.revenue, label: Text('Prihodi'), icon: Icon(Icons.payments_outlined)),
              ButtonSegment(value: _Tab.appointments, label: Text('Rezervacije'), icon: Icon(Icons.event_note_outlined)),
            ],
            selected: {tab},
            onSelectionChanged: (selection) => onTab(selection.single),
          ),
        ),
        const SizedBox(height: 16),
        if (tab == _Tab.revenue) _RevenueTables(report: revenue) else _AppointmentTables(report: appointments),
      ],
    );
  }
}

/// Net revenue and booked appointments side by side for every day or month of the range. The
/// two have different units, so appointments are drawn against their own axis on the right.
class _PeriodChart extends StatelessWidget {
  const _PeriodChart({required this.revenue, required this.booked, required this.money});

  final List<ReportPeriod> revenue;
  final List<ReportPeriod> booked;
  final MoneyFormat money;

  @override
  Widget build(BuildContext context) {
    final topRevenue = revenue.fold<double>(0, (top, p) => math.max(top, p.revenue));
    final topCount = booked.fold<int>(0, (top, p) => math.max(top, p.appointments));
    // Both axes are split into the same number of round steps, so each grid line carries a
    // round amount on the left and a round count on the right.
    final revenueStep = _niceStep(topRevenue / _steps);
    // Appointments are whole, so their step is too (2,5 becomes 5).
    var countStep = _niceStep(topCount / _steps);
    countStep = countStep < 1 ? 1 : (countStep % 1 == 0 ? countStep : countStep * 2);
    final maxY = revenueStep * _steps;
    final scale = revenueStep / countStep;
    // With many days on the axis, every label would overlap its neighbours.
    final labelEvery = (revenue.length / 12).ceil();
    const label = TextStyle(color: AppColors.textSecondary, fontSize: 11);

    return SectionCard(
      title: 'Prihodi i rezervacije',
      child: Column(
        children: [
          const Row(
            children: [
              _Legend(color: AppColors.accent, label: 'Neto prihod'),
              SizedBox(width: 20),
              _Legend(color: AppColors.success, label: 'Rezervacije'),
            ],
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 300,
            child: BarChart(
              BarChartData(
                minY: 0,
                maxY: maxY,
                gridData: FlGridData(
                  drawVerticalLine: false,
                  horizontalInterval: revenueStep,
                  getDrawingHorizontalLine: (_) => const FlLine(color: AppColors.border, strokeWidth: 0.6),
                ),
                // A day with more refunds than payments goes below zero; the chart shows it as
                // zero and the tooltip and table give the exact figure.
                barGroups: [
                  for (var i = 0; i < revenue.length; i++)
                    BarChartGroupData(
                      x: i,
                      barsSpace: 2,
                      barRods: [
                        BarChartRodData(
                          toY: math.max(0, revenue[i].revenue),
                          color: AppColors.accent,
                          width: revenue.length > 20 ? 4 : 12,
                          borderRadius: BorderRadius.circular(2),
                        ),
                        BarChartRodData(
                          toY: i < booked.length ? booked[i].appointments * scale : 0,
                          color: AppColors.success,
                          width: revenue.length > 20 ? 4 : 12,
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ],
                    ),
                ],
                borderData: FlBorderData(show: false),
                barTouchData: BarTouchData(
                  touchTooltipData: BarTouchTooltipData(
                    getTooltipColor: (_) => AppColors.surfaceRaised,
                    getTooltipItem: (group, _, rod, rodIndex) {
                      final period = revenue[group.x];
                      final text = rodIndex == 0
                          ? '${period.label}\n${money(period.revenue)}'
                          : '${group.x < booked.length ? booked[group.x].appointments : 0} rez.';
                      return BarTooltipItem(text, const TextStyle(color: AppColors.textPrimary, fontSize: 12));
                    },
                  ),
                ),
                titlesData: FlTitlesData(
                  topTitles: const AxisTitles(),
                  leftTitles: AxisTitles(
                    sideTitles: SideTitles(
                      showTitles: true,
                      reservedSize: 56,
                      interval: revenueStep,
                      getTitlesWidget: (value, meta) =>
                          SideTitleWidget(meta: meta, child: Text(meta.formattedValue, style: label)),
                    ),
                  ),
                  rightTitles: AxisTitles(
                    sideTitles: SideTitles(
                      showTitles: topCount > 0,
                      reservedSize: 36,
                      interval: revenueStep,
                      getTitlesWidget: (value, meta) =>
                          SideTitleWidget(meta: meta, child: Text('${(value / scale).round()}', style: label)),
                    ),
                  ),
                  bottomTitles: AxisTitles(
                    sideTitles: SideTitles(
                      showTitles: true,
                      reservedSize: 28,
                      getTitlesWidget: (value, meta) {
                        final index = value.toInt();
                        if (index < 0 || index >= revenue.length || index % labelEvery != 0) {
                          return const SizedBox.shrink();
                        }
                        return SideTitleWidget(meta: meta, child: Text(revenue[index].label, style: label));
                      },
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

const _steps = 5;

/// The smallest of 1, 2, 2.5 and 5 times a power of ten that is at least [raw]; never zero, so
/// an empty period still draws an axis.
double _niceStep(double raw) {
  if (raw <= 0) {
    return 1;
  }
  final magnitude = math.pow(10, (math.log(raw) / math.ln10).floor()).toDouble();
  for (final factor in const [1, 2, 2.5, 5, 10]) {
    if (factor * magnitude >= raw) {
      return factor * magnitude;
    }
  }
  return 10 * magnitude;
}

class _Legend extends StatelessWidget {
  const _Legend({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      CircleAvatar(radius: 5, backgroundColor: color),
      const SizedBox(width: 6),
      Text(label, style: const TextStyle(color: AppColors.textSecondary)),
    ],
  );
}

class _DistributionChart extends StatelessWidget {
  const _DistributionChart({required this.shares});

  final List<ReportShare> shares;

  @override
  Widget build(BuildContext context) {
    // The chart stays readable with five slices; the rest are added up.
    final shown = shares.length <= 5
        ? shares
        : [
            ...shares.take(4),
            ReportShare(
              'Ostalo',
              shares.skip(4).fold(0, (sum, s) => sum + s.count),
              shares.skip(4).fold(0, (sum, s) => sum + s.amount),
              shares.skip(4).fold(0, (sum, s) => sum + s.percent),
            ),
          ];

    return SectionCard(
      title: 'Distribucija usluga',
      child: SizedBox(
        height: 340,
        child: shown.isEmpty
            ? const EmptyView(icon: Icons.pie_chart_outline, message: 'U ovom periodu nema usluga.')
            : Column(
                children: [
                  Expanded(
                    child: PieChart(
                      PieChartData(
                        sectionsSpace: 1,
                        centerSpaceRadius: 0,
                        sections: [
                          for (var i = 0; i < shown.length; i++)
                            PieChartSectionData(
                              value: shown[i].percent,
                              color: AppColors.series[i % AppColors.series.length],
                              radius: 90,
                              title: '${shown[i].percent.toStringAsFixed(0)}%',
                              titleStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12),
                            ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  for (var i = 0; i < shown.length; i++)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 3),
                      child: Row(
                        children: [
                          CircleAvatar(radius: 5, backgroundColor: AppColors.series[i % AppColors.series.length]),
                          const SizedBox(width: 8),
                          Expanded(child: Text(shown[i].name, overflow: TextOverflow.ellipsis)),
                          Text('${shown[i].count}', style: const TextStyle(fontWeight: FontWeight.w700)),
                        ],
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}

class _RevenueTables extends ConsumerWidget {
  const _RevenueTables({required this.report});

  final RevenueReport report;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final money = ref.watch(moneyProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(child: _Figure(label: 'Bruto prihod', value: money(report.grossRevenue.value))),
            Expanded(child: _Figure(label: 'Povrati', value: money(report.refundedAmount))),
            Expanded(child: _Figure(label: 'Broj uplata', value: '${report.paymentCount}')),
            Expanded(child: _Figure(label: 'Prosječna uplata', value: money(report.averageTransaction.value))),
          ],
        ),
        const SizedBox(height: 16),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: _Titled(
                title: 'Prihod po kategoriji',
                child: SimpleTable<ReportShare>(
                  items: report.byCategory,
                  columns: [
                    TableColumn('Kategorija', (s) => Text(s.name), flex: 3),
                    TableColumn('Stavki', (s) => Text('${s.count}')),
                    TableColumn('Iznos', (s) => Text(money(s.amount)), flex: 2),
                    TableColumn('Udio', (s) => Text('${s.percent.toStringAsFixed(1)}%')),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: _Titled(
                title: 'Najprofitabilnije usluge',
                child: SimpleTable<ReportShare>(
                  items: report.topServices.take(8).toList(),
                  columns: [
                    TableColumn('Usluga', (s) => Text(s.name), flex: 3),
                    TableColumn('Stavki', (s) => Text('${s.count}')),
                    TableColumn('Iznos', (s) => Text(money(s.amount)), flex: 2),
                  ],
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _Titled(
          title: 'Povrati',
          child: SimpleTable<ReportRefund>(
            items: report.refunds,
            columns: [
              TableColumn('Plaćanje', (r) => Text(r.paymentNumber), flex: 2),
              TableColumn('Datum', (r) => Text(Fmt.dateTime(r.completedAtUtc)), flex: 2),
              TableColumn('Razlog', (r) => Text(r.reason), flex: 4),
              TableColumn('Zatražio', (r) => Text(r.requestedBy), flex: 2),
              TableColumn('Iznos', (r) => Text(money(r.amount)), flex: 2),
            ],
          ),
        ),
      ],
    );
  }
}

class _AppointmentTables extends StatelessWidget {
  const _AppointmentTables({required this.report});

  final AppointmentReport report;

  @override
  Widget build(BuildContext context) {
    final total = report.byStatus.fold(0, (sum, s) => sum + s.count);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(child: _Figure(label: 'Završeno', value: '${report.completed}')),
            Expanded(child: _Figure(label: 'Otkazano', value: '${report.cancelled}')),
            Expanded(child: _Figure(label: 'Stopa završenosti', value: '${report.completionRate.toStringAsFixed(1)}%')),
            Expanded(child: _Figure(label: 'Prosječno trajanje', value: Fmt.duration(report.averageDurationMinutes))),
          ],
        ),
        const SizedBox(height: 16),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: _Titled(
                title: 'Po statusu',
                child: SimpleTable<StatusCount>(
                  items: report.byStatus,
                  columns: [
                    TableColumn('Status', (s) => Text(s.status.label), flex: 2),
                    TableColumn('Broj', (s) => Text('${s.count}')),
                    TableColumn(
                      'Udio',
                      (s) => Text(total == 0 ? '–' : '${(100 * s.count / total).toStringAsFixed(1)}%'),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: _Titled(
                title: 'Po zaposleniku',
                child: SimpleTable<EmployeeFigures>(
                  items: report.byEmployee,
                  columns: [
                    TableColumn('Zaposlenik', (e) => Text(e.name), flex: 3),
                    TableColumn('Dodijeljeno', (e) => Text('${e.total}'), flex: 2),
                    TableColumn('Završeno', (e) => Text('${e.completed}'), flex: 2),
                    TableColumn('Stopa', (e) => Text('${e.completionRate.toStringAsFixed(1)}%'), flex: 2),
                  ],
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _Titled(
          title: 'Razlozi otkazivanja',
          child: SimpleTable<ReasonCount>(
            items: report.cancellationReasons,
            columns: [
              TableColumn('Razlog', (r) => Text(r.reason), flex: 6),
              TableColumn('Broj', (r) => Text('${r.count}')),
            ],
          ),
        ),
      ],
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(label, style: const TextStyle(color: AppColors.textSecondary)),
      const SizedBox(height: 4),
      Text(value, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
    ],
  );
}

/// A table under its own heading, with a line instead of an empty table when there is nothing.
class _Titled extends StatelessWidget {
  const _Titled({required this.title, required this.child});

  final String title;
  final SimpleTable<dynamic> child;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
      const SizedBox(height: 8),
      if (child.items.isEmpty)
        const Text('Nema podataka za izabrani period.', style: TextStyle(color: AppColors.textSecondary))
      else
        child,
    ],
  );
}
