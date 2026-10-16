import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../data/models/appointment.dart';

/// Keyed by "yyyy-mm-dd|serviceIds" so equal requests share one result.
final _slotsProvider = FutureProvider.autoDispose.family<List<Slot>, String>((ref, key) {
  ref.watch(appointmentsVersionProvider);
  final [day, services] = key.split('|');
  return ref.watch(appointmentRepositoryProvider).slots(
        DateTime.parse(day),
        [for (final id in services.split(',')) int.parse(id)],
      );
});

/// Today in garage time. Working hours are kept in UTC, so the garage's day is the UTC day.
DateTime garageToday() {
  final now = DateTime.now().toUtc();
  return DateTime.utc(now.year, now.month, now.day);
}

/// Date field and the grid of whole-hour slots for that day. The server's slot list is the one
/// definition of a free slot; it checks again when the booking is sent.
class SlotPicker extends ConsumerWidget {
  const SlotPicker({
    required this.serviceIds,
    required this.date,
    required this.selected,
    required this.onDateChanged,
    required this.onSlotChanged,
    super.key,
  });

  final List<int> serviceIds;
  final DateTime? date;
  final DateTime? selected;
  final ValueChanged<DateTime> onDateChanged;
  final ValueChanged<DateTime?> onSlotChanged;

  Future<void> _pickDate(BuildContext context, int maxDaysAhead) async {
    final today = garageToday();
    final picked = await showDatePicker(
      context: context,
      initialDate: date ?? today,
      firstDate: today,
      lastDate: today.add(Duration(days: maxDaysAhead)),
      helpText: 'Izaberite datum',
      cancelText: 'Odustani',
      confirmText: 'Izaberi',
    );
    if (picked != null) {
      onDateChanged(DateTime.utc(picked.year, picked.month, picked.day));
      onSlotChanged(null);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final maxDaysAhead = ref.watch(lookupProvider).value?.garage.bookingMaxDaysAhead ?? 90;
    final day = date;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('Izaberite datum', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
        const SizedBox(height: 10),
        InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          onTap: () => _pickDate(context, maxDaysAhead),
          child: InputDecorator(
            decoration: const InputDecoration(suffixIcon: Icon(Icons.calendar_month_outlined)),
            child: Text(day == null ? 'Dodirnite za izbor datuma' : Fmt.dayMonth(day)),
          ),
        ),
        const SizedBox(height: 20),
        const Text('Izaberite vrijeme', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
        const SizedBox(height: 10),
        if (day == null)
          const Text('Prvo izaberite datum.', style: TextStyle(color: AppColors.textSecondary))
        else
          _SlotGrid(
            slotsKey: '${day.toIso8601String().substring(0, 10)}|${serviceIds.join(',')}',
            selected: selected,
            onSelected: onSlotChanged,
          ),
      ],
    );
  }
}

class _SlotGrid extends ConsumerWidget {
  const _SlotGrid({required this.slotsKey, required this.selected, required this.onSelected});

  final String slotsKey;
  final DateTime? selected;
  final ValueChanged<DateTime?> onSelected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final slots = ref.watch(_slotsProvider(slotsKey));

    return slots.when(
      loading: () => const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => ErrorView(
        message: 'Termine nije moguće učitati.',
        onRetry: () => ref.invalidate(_slotsProvider(slotsKey)),
      ),
      data: (items) {
        if (items.isEmpty) {
          return const Text('Za ovaj dan nema termina.', style: TextStyle(color: AppColors.textSecondary));
        }

        final free = items.where((s) => s.isAvailable).toList();
        final reasons = {for (final s in items) ?s.unavailableReason};

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            GridView.count(
              crossAxisCount: 3,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
              childAspectRatio: 2.1,
              children: [
                for (final slot in items) _SlotButton(slot: slot, selected: slot.startUtc == selected, onTap: onSelected),
              ],
            ),
            if (free.isEmpty && reasons.isNotEmpty) BlockedReason(reasons.join(' ')),
          ],
        );
      },
    );
  }
}

class _SlotButton extends StatelessWidget {
  const _SlotButton({required this.slot, required this.selected, required this.onTap});

  final Slot slot;
  final bool selected;
  final ValueChanged<DateTime?> onTap;

  @override
  Widget build(BuildContext context) {
    final button = Material(
      color: selected ? AppColors.accent : AppColors.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: BorderSide(color: selected ? AppColors.accent : AppColors.border),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: slot.isAvailable ? () => onTap(slot.startUtc) : null,
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                Fmt.time(slot.startUtc),
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 16,
                  color: slot.isAvailable ? AppColors.textPrimary : AppColors.textSecondary.withValues(alpha: 0.5),
                  decoration: slot.isAvailable ? null : TextDecoration.lineThrough,
                ),
              ),
              if (slot.isAvailable && !selected)
                Text('${slot.freePlaces} slob.', style: const TextStyle(color: AppColors.textSecondary, fontSize: 11)),
            ],
          ),
        ),
      ),
    );

    final reason = slot.unavailableReason;
    return reason == null ? button : Tooltip(message: reason, child: button);
  }
}
