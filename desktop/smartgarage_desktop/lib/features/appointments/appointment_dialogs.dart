import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/appointment.dart';
import '../../data/models/employee.dart';

/// Shared frame for the action dialogs: title with a close button, content, and the server's
/// error shown above the buttons.
class ActionDialog extends StatelessWidget {
  const ActionDialog({
    required this.title,
    required this.content,
    required this.confirmLabel,
    required this.onConfirm,
    required this.busy,
    this.error,
    this.width = 460,
    super.key,
  });

  final String title;
  final Widget content;
  final String confirmLabel;
  final VoidCallback? onConfirm;
  final bool busy;
  final String? error;
  final double width;

  @override
  Widget build(BuildContext context) => AlertDialog(
    titlePadding: const EdgeInsets.fromLTRB(24, 16, 12, 0),
    title: Row(
      children: [
        Expanded(child: Text(title)),
        IconButton(
          tooltip: 'Zatvori',
          onPressed: () => Navigator.of(context).pop(false),
          icon: const Icon(Icons.close),
        ),
      ],
    ),
    content: SizedBox(
      width: width,
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            content,
            if (error != null) Padding(padding: const EdgeInsets.only(top: 12), child: FormErrorText(error)),
          ],
        ),
      ),
    ),
    actions: [
      TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
      SizedBox(
        width: 160,
        child: BusyButton(label: confirmLabel, busy: busy, onPressed: onConfirm),
      ),
    ],
  );
}

final _assignableProvider = FutureProvider.autoDispose<List<Employee>>((ref) async {
  final page = await ref.watch(employeeRepositoryProvider).list(isActive: true, size: 100);
  return page.items.where((e) => e.isAvailableForAssignment).toList();
});

class AssignDialog extends ConsumerStatefulWidget {
  const AssignDialog({required this.appointment, super.key});

  final AppointmentDetail appointment;

  @override
  ConsumerState<AssignDialog> createState() => _AssignDialogState();
}

class _AssignDialogState extends ConsumerState<AssignDialog> {
  int? _employeeId;
  var _busy = false;
  String? _error;

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(appointmentRepositoryProvider).assign(widget.appointment.id, _employeeId!);
      if (mounted) {
        showMessage(context, 'Mehaničar je dodijeljen rezervaciji ${widget.appointment.number}.');
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (error) {
      setState(() => _error = error.fieldErrors.values.firstOrNull ?? error.message);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final employees = ref.watch(_assignableProvider);

    return ActionDialog(
      title: 'Dodjela mehaničara',
      confirmLabel: 'Dodijeli',
      busy: _busy,
      error: _error,
      onConfirm: _employeeId == null ? null : _save,
      content: employees.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Text(errorMessage(e)),
        data: (list) => list.isEmpty
            ? const Text('Nema aktivnih zaposlenika dostupnih za dodjelu.')
            : DropdownButtonFormField<int>(
                initialValue: _employeeId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Mehaničar'),
                items: [
                  for (final e in list) DropdownMenuItem(value: e.id, child: Text('${e.fullName} — ${e.position}')),
                ],
                onChanged: (v) => setState(() => _employeeId = v),
              ),
      ),
    );
  }
}

class CompleteDialog extends ConsumerStatefulWidget {
  const CompleteDialog({required this.appointment, super.key});

  final AppointmentDetail appointment;

  @override
  ConsumerState<CompleteDialog> createState() => _CompleteDialogState();
}

class _CompleteDialogState extends ConsumerState<CompleteDialog> {
  final _form = GlobalKey<FormState>();
  final _mileage = TextEditingController();
  final _notes = TextEditingController();
  var _busy = false;
  String? _error;
  Map<String, String> _fieldErrors = const {};

  @override
  void dispose() {
    _mileage.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
      _fieldErrors = const {};
    });
    try {
      await ref
          .read(appointmentRepositoryProvider)
          .complete(
            widget.appointment.id,
            mileageKm: int.parse(_mileage.text.trim()),
            internalNotes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
          );
      if (mounted) {
        showMessage(context, 'Servis ${widget.appointment.number} je završen. Kupac sada može platiti.');
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (error) {
      setState(() {
        _fieldErrors = error.fieldErrors;
        _error = error.fieldErrors.isEmpty ? error.message : null;
      });
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) => ActionDialog(
    title: 'Završetak servisa',
    confirmLabel: 'Završi servis',
    busy: _busy,
    error: _error,
    onConfirm: _save,
    content: Form(
      key: _form,
      child: Column(
        children: [
          TextFormField(
            controller: _mileage,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(
              labelText: 'Kilometraža vozila (km)',
              helperText: 'Očitana na vozilu pri završetku servisa.',
            ),
            validator: Validators.mileage,
            forceErrorText: _fieldErrors['mileageAtAppointmentKm'],
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _notes,
            maxLines: 3,
            maxLength: 2000,
            decoration: const InputDecoration(labelText: 'Interna napomena (nije obavezna)'),
            forceErrorText: _fieldErrors['internalNotes'],
          ),
        ],
      ),
    ),
  );
}

final _slotsProvider = FutureProvider.autoDispose.family<List<Slot>, String>((ref, key) {
  final [day, services] = key.split('|');
  return ref.watch(appointmentRepositoryProvider).slots(DateTime.parse(day), [
    for (final id in services.split(',')) int.parse(id),
  ]);
});

class RescheduleDialog extends ConsumerStatefulWidget {
  const RescheduleDialog({required this.appointment, super.key});

  final AppointmentDetail appointment;

  @override
  ConsumerState<RescheduleDialog> createState() => _RescheduleDialogState();
}

class _RescheduleDialogState extends ConsumerState<RescheduleDialog> {
  final _reason = TextEditingController();
  DateTime? _date;
  DateTime? _slot;
  var _busy = false;
  String? _error;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now().toUtc();
    final today = DateTime.utc(now.year, now.month, now.day);
    final maxDays = ref.read(lookupProvider).value?.garage.bookingMaxDaysAhead ?? 90;
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? today,
      firstDate: today,
      lastDate: today.add(Duration(days: maxDays)),
    );
    if (picked != null) {
      setState(() {
        _date = DateTime.utc(picked.year, picked.month, picked.day);
        _slot = null;
      });
    }
  }

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(appointmentRepositoryProvider)
          .reschedule(widget.appointment.id, _slot!, _reason.text.trim().isEmpty ? null : _reason.text.trim());
      if (mounted) {
        showMessage(context, 'Termin je pomjeren na ${Fmt.dateTime(_slot!)}. Kupac je obaviješten.');
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (error) {
      setState(() {
        _error = error.fieldErrors.values.firstOrNull ?? error.message;
        _slot = null;
      });
      ref.invalidate(_slotsProvider);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final date = _date;
    final services = widget.appointment.items.map((i) => i.serviceId).join(',');

    return ActionDialog(
      title: 'Pomjeranje termina',
      confirmLabel: 'Pomjeri',
      width: 560,
      busy: _busy,
      error: _error,
      onConfirm: _slot == null ? null : _save,
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Trenutni termin: ${Fmt.dateTime(widget.appointment.startUtc)}'),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: _pickDate,
            icon: const Icon(Icons.calendar_month_outlined, size: 18),
            label: Text(date == null ? 'Izaberite datum' : Fmt.dayMonth(date)),
          ),
          const SizedBox(height: 16),
          if (date != null)
            ref
                .watch(_slotsProvider('${date.toIso8601String().substring(0, 10)}|$services'))
                .when(
                  loading: () => const Center(child: CircularProgressIndicator()),
                  error: (e, _) => Text(errorMessage(e)),
                  data: (slots) => Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final slot in slots)
                        Tooltip(
                          message: slot.unavailableReason ?? 'Slobodnih mjesta: ${slot.freePlaces}',
                          child: ChoiceChip(
                            label: Text(Fmt.time(slot.startUtc)),
                            selected: slot.startUtc == _slot,
                            onSelected: slot.isAvailable ? (_) => setState(() => _slot = slot.startUtc) : null,
                          ),
                        ),
                    ],
                  ),
                ),
          const SizedBox(height: 16),
          TextField(
            controller: _reason,
            maxLength: 400,
            decoration: const InputDecoration(labelText: 'Razlog (nije obavezan, vidi ga kupac)'),
          ),
          const Text(
            'Status rezervacije ostaje isti; promjena se upisuje u historiju.',
            style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
          ),
        ],
      ),
    );
  }
}
