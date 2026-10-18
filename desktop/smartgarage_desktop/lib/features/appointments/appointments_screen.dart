import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/paged_view.dart';
import '../../core/widgets/status.dart';
import '../../data/models/appointment.dart';
import '../../data/repositories.dart';

final _summaryProvider = FutureProvider.autoDispose<AppointmentSummary>((ref) {
  ref.watch(appointmentsVersionProvider);
  return ref.watch(appointmentRepositoryProvider).summary();
});

/// The master list; each row opens the appointment's detail.
class AppointmentsScreen extends ConsumerStatefulWidget {
  const AppointmentsScreen({super.key});

  @override
  ConsumerState<AppointmentsScreen> createState() => _AppointmentsScreenState();
}

class _AppointmentsScreenState extends ConsumerState<AppointmentsScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _query = const AppointmentQuery();

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _update({
    String? search,
    AppointmentStatus? status,
    bool clearStatus = false,
    DateTimeRange? range,
    bool clearRange = false,
  }) => setState(() {
    _query = AppointmentQuery(
      search: search ?? _query.search,
      status: clearStatus ? null : status ?? _query.status,
      from: clearRange ? null : range?.start ?? _query.from,
      // The range ends at the end of its last day.
      to: clearRange ? null : (range == null ? _query.to : range.end.add(const Duration(hours: 23, minutes: 59))),
    );
  });

  Future<void> _pickRange() async {
    final now = DateTime.now();
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(now.year - 3),
      lastDate: DateTime(now.year + 1, 12, 31),
      initialDateRange: _query.from == null ? null : DateTimeRange(start: _query.from!, end: _query.to ?? _query.from!),
      helpText: 'Period termina',
      saveText: 'Primijeni',
    );
    if (picked != null) {
      _update(
        range: DateTimeRange(
          start: DateTime.utc(picked.start.year, picked.start.month, picked.start.day),
          end: DateTime.utc(picked.end.year, picked.end.month, picked.end.day),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final summary = ref.watch(_summaryProvider).value;
    final version = ref.watch(appointmentsVersionProvider);
    final repository = ref.watch(appointmentRepositoryProvider);
    final money = ref.watch(moneyProvider);
    final range = _query.from == null ? null : '${Fmt.date(_query.from!)} – ${Fmt.date(_query.to!)}';

    return ScreenScaffold(
      title: 'Upravljanje rezervacijama',
      subtitle: 'Pregledajte i upravljajte rezervacijama',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: StatCard(label: 'Ukupno', value: '${summary?.total ?? '–'}'),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(
                  label: 'Potvrđeno',
                  value: '${summary?.confirmed ?? '–'}',
                  valueColor: AppColors.accent,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(
                  label: 'Na čekanju',
                  value: '${summary?.pending ?? '–'}',
                  valueColor: AppColors.warning,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(
                  label: 'Završeno',
                  value: '${summary?.completed ?? '–'}',
                  valueColor: AppColors.success,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      decoration: const InputDecoration(
                        hintText: 'Pretraži po broju, kupcu, registraciji ili usluzi...',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: (value) {
                        _debounce?.cancel();
                        _debounce = Timer(const Duration(milliseconds: 400), () => _update(search: value));
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 190,
                    child: DropdownButtonFormField<AppointmentStatus?>(
                      initialValue: _query.status,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: [
                        const DropdownMenuItem(child: Text('Svi statusi')),
                        for (final s in AppointmentStatus.values) DropdownMenuItem(value: s, child: Text(s.label)),
                      ],
                      onChanged: (s) => _update(status: s, clearStatus: s == null),
                    ),
                  ),
                  const SizedBox(width: 12),
                  OutlinedButton.icon(
                    onPressed: _pickRange,
                    icon: const Icon(Icons.date_range, size: 18),
                    label: Text(range ?? 'Period'),
                  ),
                  if (range != null)
                    IconButton(
                      tooltip: 'Poništi period',
                      onPressed: () => _update(clearRange: true),
                      icon: const Icon(Icons.close),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<AppointmentListItem>(
              reloadKey: (_query, version),
              fetch: (page) => repository.list(_query, page: page),
              emptyMessage: 'Nijedna rezervacija ne odgovara filterima.',
              builder: (context, items, _) => SimpleTable<AppointmentListItem>(
                items: items,
                onTap: (item) => context.push('/appointments/${item.id}'),
                columns: [
                  TableColumn('Broj', (a) => Text(a.number, style: const TextStyle(color: AppColors.accent)), flex: 2),
                  TableColumn(
                    'Korisnik',
                    (a) => Text(a.customerFullName, style: const TextStyle(fontWeight: FontWeight.w700)),
                    flex: 2,
                  ),
                  TableColumn('Usluga', (a) => Text(a.services, overflow: TextOverflow.ellipsis, maxLines: 2), flex: 2),
                  TableColumn('Vozilo', (a) => Text(a.vehicleDisplay, overflow: TextOverflow.ellipsis), flex: 2),
                  TableColumn(
                    'Datum & vrijeme',
                    (a) => Row(
                      children: [
                        const Icon(Icons.calendar_today_outlined, size: 14, color: AppColors.textSecondary),
                        const SizedBox(width: 6),
                        Flexible(child: Text(Fmt.dateTime(a.startUtc))),
                      ],
                    ),
                    flex: 2,
                  ),
                  TableColumn('Mehaničar', (a) => Text(a.assignedEmployeeName ?? '—'), flex: 2),
                  TableColumn(
                    'Cijena',
                    (a) => Text(money(a.totalPrice), style: const TextStyle(fontWeight: FontWeight.w700)),
                  ),
                  TableColumn(
                    'Status',
                    (a) => StatusTag(label: a.status.label, color: a.status.color, icon: a.status.icon),
                    flex: 2,
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
