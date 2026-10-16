import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/paged_list.dart';
import '../../core/widgets/status.dart';
import '../../data/models/appointment.dart';
import 'appointment_card.dart';

/// The master list of the master-detail form; each card opens the appointment's detail.
class HistoryScreen extends ConsumerStatefulWidget {
  const HistoryScreen({super.key});

  @override
  ConsumerState<HistoryScreen> createState() => _HistoryScreenState();
}

class _HistoryScreenState extends ConsumerState<HistoryScreen> {
  AppointmentStatus? _status;

  @override
  Widget build(BuildContext context) {
    final summary = ref.watch(appointmentSummaryProvider).value;
    final version = ref.watch(appointmentsVersionProvider);
    final repository = ref.watch(appointmentRepositoryProvider);
    final money = ref.watch(moneyProvider);

    return Scaffold(
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const PageHeader(title: 'Historija rezervacija', subtitle: 'Pregled svih vaših rezervacija'),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                children: [
                  _Counter(label: 'Ukupno', value: summary?.total, color: AppColors.textPrimary),
                  const SizedBox(width: 10),
                  _Counter(label: 'Završeno', value: summary?.completed, color: AppColors.success),
                  const SizedBox(width: 10),
                  _Counter(label: 'Aktivno', value: summary?.active, color: AppColors.accent),
                ],
              ),
            ),
            SizedBox(
              height: 56,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                children: [
                  _chip('Sve', null),
                  for (final status in AppointmentStatus.values) _chip(status.label, status),
                ],
              ),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: PagedList<AppointmentListItem>(
                reloadKey: (_status, version),
                fetch: (page) => repository.list(status: _status, page: page),
                empty: EmptyView(
                  icon: Icons.event_busy_outlined,
                  message: _status == null
                      ? 'Još nemate rezervacija. Zakažite prvi termin u kartici Zakaži.'
                      : 'Nema rezervacija sa statusom "${_status!.label}".',
                ),
                itemBuilder: (context, item, _) => AppointmentCard(item: item, money: money),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _chip(String label, AppointmentStatus? status) => Padding(
        padding: const EdgeInsets.only(right: 8),
        child: ChoiceChip(
          label: Text(label),
          selected: _status == status,
          avatar: status == null ? null : Icon(status.icon, size: 16, color: status.color),
          onSelected: (_) => setState(() => _status = status),
        ),
      );
}

class _Counter extends StatelessWidget {
  const _Counter({required this.label, required this.value, required this.color});

  final String label;
  final int? value;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Card(
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 16),
            child: Column(
              children: [
                Text(
                  value?.toString() ?? '–',
                  style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800, color: color),
                ),
                const SizedBox(height: 4),
                Text(label, style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
        ),
      );
}
