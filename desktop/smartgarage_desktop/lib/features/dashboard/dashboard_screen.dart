import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/status.dart';
import '../../data/models/admin.dart';
import '../../data/models/appointment.dart';

typedef _DashboardData = (Dashboard figures, List<AppointmentListItem> upcoming);

final _dashboardProvider = FutureProvider.autoDispose<_DashboardData>((ref) {
  ref.watch(appointmentsVersionProvider);
  final now = DateTime.now().toUtc();
  return (ref.watch(dashboardRepositoryProvider).get(), ref.watch(appointmentRepositoryProvider).upcoming(now)).wait;
});

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(_dashboardProvider);
    final money = ref.watch(moneyProvider);
    final number = NumberFormat.decimalPattern(appLocale);

    return ScreenScaffold(
      title: 'Dashboard',
      subtitle: 'Dobro došli nazad! Pregled vašeg poslovanja',
      actions: [
        OutlinedButton.icon(
          onPressed: () => ref.invalidate(_dashboardProvider),
          icon: const Icon(Icons.refresh, size: 18),
          label: const Text('Osvježi'),
        ),
      ],
      body: AsyncBody<_DashboardData>(
        value: data,
        onRetry: () => ref.invalidate(_dashboardProvider),
        builder: (data) {
          final (figures, upcoming) = data;
          final revenue = figures.monthRevenue;
          final byMonth = figures.revenueByMonth;

          return ListView(
            padding: const EdgeInsets.only(bottom: 28),
            children: [
              Row(
                children: [
                  Expanded(
                    child: StatCard(
                      label: 'Ukupno korisnika',
                      value: number.format(figures.customers.value),
                      icon: Icons.people_outline,
                      changePercent: figures.customers.changePercent,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: StatCard(
                      label: 'Aktivne rezervacije',
                      value: number.format(figures.activeAppointments.value),
                      icon: Icons.event_outlined,
                      color: AppColors.success,
                      changePercent: figures.activeAppointments.changePercent,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: StatCard(
                      label: 'Održane usluge',
                      value: number.format(figures.completedServices.value),
                      icon: Icons.build_outlined,
                      color: AppColors.warning,
                      changePercent: figures.completedServices.changePercent,
                    ),
                  ),
                  if (revenue != null) ...[
                    const SizedBox(width: 16),
                    Expanded(
                      child: StatCard(
                        label: 'Prihod (mjesec)',
                        value: money(revenue.value),
                        icon: Icons.payments_outlined,
                        changePercent: revenue.changePercent,
                      ),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 20),
              SizedBox(
                height: 420,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (byMonth != null) ...[
                      Expanded(
                        flex: 2,
                        child: _RevenueChart(months: byMonth, money: money),
                      ),
                      const SizedBox(width: 16),
                    ],
                    Expanded(child: _DistributionChart(shares: figures.serviceDistribution)),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              _Upcoming(items: upcoming),
            ],
          );
        },
      ),
    );
  }
}

class _RevenueChart extends StatelessWidget {
  const _RevenueChart({required this.months, required this.money});

  final List<MonthlyRevenue> months;
  final MoneyFormat money;

  @override
  Widget build(BuildContext context) {
    final monthName = DateFormat('MMM', appLocale);
    final top = months.fold<double>(0, (max, m) => m.revenue > max ? m.revenue : max);

    return SectionCard(
      title: 'Prihodi po mjesecima',
      child: SizedBox(
        height: 320,
        child: LineChart(
          LineChartData(
            minY: 0,
            maxY: top == 0 ? 100 : top * 1.2,
            gridData: FlGridData(
              getDrawingHorizontalLine: (_) => const FlLine(color: AppColors.border, strokeWidth: 0.6),
              getDrawingVerticalLine: (_) => const FlLine(color: AppColors.border, strokeWidth: 0.6),
            ),
            borderData: FlBorderData(show: false),
            lineTouchData: LineTouchData(
              touchTooltipData: LineTouchTooltipData(
                getTooltipItems: (spots) => [
                  for (final spot in spots)
                    LineTooltipItem(money(spot.y), const TextStyle(color: AppColors.textPrimary)),
                ],
              ),
            ),
            titlesData: FlTitlesData(
              topTitles: const AxisTitles(),
              rightTitles: const AxisTitles(),
              leftTitles: AxisTitles(
                sideTitles: SideTitles(
                  showTitles: true,
                  reservedSize: 64,
                  // The top of the axis is 20 % above the highest month and rarely a round
                  // number; its label would crowd the one just under it.
                  getTitlesWidget: (value, meta) => value == meta.max && value % meta.appliedInterval != 0
                      ? const SizedBox.shrink()
                      : SideTitleWidget(
                          meta: meta,
                          child: Text(
                            meta.formattedValue,
                            style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                          ),
                        ),
                ),
              ),
              bottomTitles: AxisTitles(
                sideTitles: SideTitles(
                  showTitles: true,
                  interval: 1,
                  getTitlesWidget: (value, meta) {
                    final index = value.toInt();
                    if (index < 0 || index >= months.length || value != index) {
                      return const SizedBox.shrink();
                    }
                    final month = months[index];
                    return SideTitleWidget(
                      meta: meta,
                      child: Text(
                        monthName.format(DateTime(month.year, month.month)),
                        style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                      ),
                    );
                  },
                ),
              ),
            ),
            lineBarsData: [
              LineChartBarData(
                spots: [for (var i = 0; i < months.length; i++) FlSpot(i.toDouble(), months[i].revenue)],
                isCurved: true,
                preventCurveOverShooting: true,
                color: AppColors.accent,
                barWidth: 3,
                belowBarData: BarAreaData(show: true, color: AppColors.accent.withValues(alpha: 0.08)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DistributionChart extends StatelessWidget {
  const _DistributionChart({required this.shares});

  final List<ServiceShare> shares;

  @override
  Widget build(BuildContext context) => SectionCard(
    title: 'Distribucija usluga',
    child: shares.isEmpty
        ? const SizedBox(
            height: 320,
            child: EmptyView(icon: Icons.pie_chart_outline, message: 'Još nema završenih usluga.'),
          )
        : SizedBox(
            height: 320,
            child: Column(
              children: [
                Expanded(
                  child: PieChart(
                    PieChartData(
                      sectionsSpace: 1,
                      centerSpaceRadius: 0,
                      sections: [
                        for (var i = 0; i < shares.length; i++)
                          PieChartSectionData(
                            value: shares[i].percent,
                            color: AppColors.series[i % AppColors.series.length],
                            radius: 90,
                            title: '${shares[i].percent.toStringAsFixed(0)}%',
                            titleStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12),
                          ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                for (var i = 0; i < shares.length; i++)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 3),
                    child: Row(
                      children: [
                        CircleAvatar(radius: 5, backgroundColor: AppColors.series[i % AppColors.series.length]),
                        const SizedBox(width: 8),
                        Expanded(child: Text(shares[i].name, overflow: TextOverflow.ellipsis)),
                        Text(
                          '${shares[i].percent.toStringAsFixed(1)}%',
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
  );
}

class _Upcoming extends StatelessWidget {
  const _Upcoming({required this.items});

  final List<AppointmentListItem> items;

  @override
  Widget build(BuildContext context) => SectionCard(
    title: 'Nadolazeće rezervacije',
    child: items.isEmpty
        ? const Text('Nema zakazanih termina.', style: TextStyle(color: AppColors.textSecondary))
        : Column(
            children: [
              for (final item in items)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  onTap: () => context.push('/appointments/${item.id}'),
                  leading: const CircleAvatar(
                    backgroundColor: AppColors.accentSoft,
                    child: Icon(Icons.person_outline, color: AppColors.accent),
                  ),
                  title: Text(item.customerFullName, style: const TextStyle(fontWeight: FontWeight.w700)),
                  subtitle: Text(item.services, style: const TextStyle(color: AppColors.textSecondary)),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(Fmt.dateTime(item.startUtc)),
                      const SizedBox(width: 16),
                      SizedBox(
                        width: 120,
                        child: Align(
                          alignment: Alignment.centerRight,
                          child: Pill(label: item.status.label, color: item.status.color),
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
  );
}
