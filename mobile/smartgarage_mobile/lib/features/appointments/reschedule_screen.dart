import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/appointment.dart';
import '../booking/slot_picker.dart';

/// A new time for the same services. The status stays as it is; the change is kept in the
/// appointment's history.
class RescheduleScreen extends ConsumerStatefulWidget {
  const RescheduleScreen({required this.appointmentId, super.key});

  final int appointmentId;

  @override
  ConsumerState<RescheduleScreen> createState() => _RescheduleScreenState();
}

class _RescheduleScreenState extends ConsumerState<RescheduleScreen> {
  static const _reasonMaxLength = 400;

  final _reason = TextEditingController();
  DateTime? _date;
  DateTime? _slot;
  var _sending = false;
  String? _error;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _submit(AppointmentDetail appointment) async {
    final slot = _slot!;
    final confirmed = await confirm(
      context,
      title: 'Promijeniti termin?',
      message: 'Rezervacija ${appointment.number} prelazi sa ${Fmt.dateTime(appointment.startUtc)} '
          'na ${Fmt.dayMonth(slot)} u ${Fmt.time(slot)}.',
      confirmLabel: 'Promijeni',
    );
    if (!confirmed || !mounted) {
      return;
    }

    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await ref.read(appointmentRepositoryProvider).reschedule(
            appointment.id,
            slot,
            _reason.text.trim().isEmpty ? null : _reason.text.trim(),
          );
      ref.read(appointmentsVersionProvider.notifier).bump();
      if (mounted) {
        showMessage(context, 'Termin je promijenjen na ${Fmt.dayMonth(slot)} u ${Fmt.time(slot)}.');
        Navigator.of(context).pop();
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.fieldErrors.values.firstOrNull ?? error.message;
          _slot = null;
        });
        ref.read(appointmentsVersionProvider.notifier).bump();
      }
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(appointmentDetailProvider(widget.appointmentId));

    return Scaffold(
      appBar: AppBar(title: const Text('Promjena termina')),
      body: AsyncBody<AppointmentDetail>(
        value: detail,
        onRetry: () => ref.invalidate(appointmentDetailProvider(widget.appointmentId)),
        builder: (appointment) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SectionCard(
              child: Column(
                children: [
                  KeyValueRow(label: 'Rezervacija', value: appointment.number),
                  KeyValueRow(label: 'Trenutni termin', value: Fmt.dateTime(appointment.startUtc)),
                  KeyValueRow(label: 'Usluge', value: appointment.serviceNames.join('\n')),
                ],
              ),
            ),
            const SizedBox(height: 20),
            SlotPicker(
              serviceIds: [for (final item in appointment.items) item.serviceId],
              date: _date,
              selected: _slot,
              onDateChanged: (d) => setState(() => _date = d),
              onSlotChanged: (s) => setState(() => _slot = s),
            ),
            const SizedBox(height: 20),
            TextField(
              controller: _reason,
              maxLength: _reasonMaxLength,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Razlog promjene (nije obavezan)'),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
              ),
            BusyButton(
              label: 'Potvrdi novi termin',
              busy: _sending,
              onPressed: _slot == null ? null : () => _submit(appointment),
            ),
          ],
        ),
      ),
    );
  }
}
