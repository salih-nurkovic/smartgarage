import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../data/models/employee.dart';
import '../appointments/appointment_dialogs.dart';

/// A new employee gets an account and a work profile together; editing changes the profile and
/// contact details only - the password is the employee's own business.
class EmployeeFormDialog extends ConsumerStatefulWidget {
  const EmployeeFormDialog({this.employee, super.key});

  final Employee? employee;

  @override
  ConsumerState<EmployeeFormDialog> createState() => _EmployeeFormDialogState();
}

class _EmployeeFormDialogState extends ConsumerState<EmployeeFormDialog> with ServerFormState {
  late final Employee? _e = widget.employee;
  late final _userName = TextEditingController(text: _e?.userName);
  late final _firstName = TextEditingController(text: _e?.firstName);
  late final _lastName = TextEditingController(text: _e?.lastName);
  late final _email = TextEditingController(text: _e?.email);
  late final _phone = TextEditingController(text: _e?.phoneNumber);
  late final _position = TextEditingController(text: _e?.position);
  late final _specialization = TextEditingController(text: _e?.specialization);
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  late DateTime _hireDate = _e?.hireDateUtc ?? DateTime.now().toUtc();
  late bool _available = _e?.isAvailableForAssignment ?? true;

  bool get _editing => _e != null;

  @override
  void dispose() {
    for (final c in [
      _userName,
      _firstName,
      _lastName,
      _email,
      _phone,
      _position,
      _specialization,
      _password,
      _confirm,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _pickHireDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _hireDate,
      firstDate: DateTime(1970),
      lastDate: DateTime.now(),
      helpText: 'Datum zaposlenja',
    );
    if (picked != null) {
      setState(() => _hireDate = picked);
    }
  }

  Future<void> _save() async {
    final hireDate =
        '${_hireDate.year}-${_hireDate.month.toString().padLeft(2, '0')}-${_hireDate.day.toString().padLeft(2, '0')}';
    final specialization = _specialization.text.trim().isEmpty ? null : _specialization.text.trim();
    final repository = ref.read(employeeRepositoryProvider);

    final saved = await submit(() async {
      if (_editing) {
        await repository.update(_e!.id, {
          'email': _email.text.trim(),
          'firstName': _firstName.text.trim(),
          'lastName': _lastName.text.trim(),
          'phoneNumber': _phone.text.trim(),
          'position': _position.text.trim(),
          'specialization': specialization,
          'hireDate': hireDate,
          'isAvailableForAssignment': _available,
        });
      } else {
        await repository.create({
          'userName': _userName.text.trim(),
          'email': _email.text.trim(),
          'firstName': _firstName.text.trim(),
          'lastName': _lastName.text.trim(),
          'phoneNumber': _phone.text.trim(),
          'password': _password.text,
          'confirmPassword': _confirm.text,
          'position': _position.text.trim(),
          'specialization': specialization,
          'hireDate': hireDate,
        });
      }
    });

    if (saved && mounted) {
      showMessage(
        context,
        _editing
            ? 'Podaci zaposlenika su sačuvani.'
            : 'Zaposlenik ${_firstName.text.trim()} ${_lastName.text.trim()} je dodan. '
                  'Radi radnim danima u radno vrijeme servisa dok mu ne promijenite raspored.',
      );
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) => ActionDialog(
    title: _editing ? 'Uredi zaposlenika' : 'Novi zaposlenik',
    confirmLabel: 'Sačuvaj',
    width: 640,
    busy: busy,
    error: formError,
    onConfirm: _save,
    content: Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(child: _field(_firstName, 'firstName', 'Ime', (v) => Validators.personName(v, 'Ime'))),
              const SizedBox(width: 12),
              Expanded(child: _field(_lastName, 'lastName', 'Prezime', (v) => Validators.personName(v, 'Prezime'))),
            ],
          ),
          if (!_editing) _field(_userName, 'userName', 'Korisničko ime', Validators.userName),
          Row(
            children: [
              Expanded(child: _field(_email, 'email', 'E-mail adresa', Validators.email)),
              const SizedBox(width: 12),
              Expanded(child: _field(_phone, 'phoneNumber', 'Broj telefona', Validators.phone)),
            ],
          ),
          Row(
            children: [
              Expanded(child: _field(_position, 'position', 'Pozicija', (v) => Validators.required(v, 'Pozicija'))),
              const SizedBox(width: 12),
              Expanded(
                child: _field(_specialization, 'specialization', 'Specijalizacija (nije obavezna)', (_) => null),
              ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: InkWell(
              onTap: _pickHireDate,
              child: InputDecorator(
                decoration: InputDecoration(
                  labelText: 'Datum zaposlenja',
                  suffixIcon: const Icon(Icons.calendar_month_outlined),
                  errorText: serverError('hireDate'),
                ),
                child: Text(Fmt.date(_hireDate)),
              ),
            ),
          ),
          if (!_editing) ...[
            _field(_password, 'password', 'Početna lozinka', Validators.password, obscure: true),
            _field(
              _confirm,
              'confirmPassword',
              'Potvrda lozinke',
              (v) => Validators.confirmPassword(v, _password.text),
              obscure: true,
            ),
          ],
          if (_editing)
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Prima nove poslove'),
              subtitle: const Text('Isključite npr. za vrijeme godišnjeg odmora.'),
              value: _available,
              onChanged: (v) => setState(() => _available = v),
            ),
        ],
      ),
    ),
  );

  Widget _field(
    TextEditingController controller,
    String field,
    String label,
    String? Function(String?) validator, {
    bool obscure = false,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: TextFormField(
      controller: controller,
      obscureText: obscure,
      decoration: InputDecoration(labelText: label),
      validator: validator,
      forceErrorText: serverError(field),
      onChanged: (_) => clearServerError(field),
    ),
  );
}

/// The week as rows: working-day switch, start and end time.
class ScheduleDialog extends ConsumerStatefulWidget {
  const ScheduleDialog({required this.employee, super.key});

  final Employee employee;

  @override
  ConsumerState<ScheduleDialog> createState() => _ScheduleDialogState();
}

class _ScheduleDialogState extends ConsumerState<ScheduleDialog> {
  late List<ScheduleDay> _days = [
    for (final day in WeekDay.values)
      widget.employee.schedule.where((d) => d.day == day).firstOrNull ??
          ScheduleDay(day: day, start: 8 * 60, end: 17 * 60, isWorkingDay: false),
  ];
  var _busy = false;
  String? _error;

  String? get _invalid {
    for (final day in _days) {
      if (day.isWorkingDay && day.end <= day.start) {
        return '${day.day.label}: kraj radnog vremena mora biti poslije početka.';
      }
    }
    return null;
  }

  Future<void> _pick(int index, {required bool start}) async {
    final day = _days[index];
    final current = start ? day.start : day.end;
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: current ~/ 60, minute: current % 60),
      builder: (context, child) =>
          MediaQuery(data: MediaQuery.of(context).copyWith(alwaysUse24HourFormat: true), child: child!),
    );
    if (picked != null) {
      final minutes = picked.hour * 60 + picked.minute;
      setState(() {
        _days = [..._days]..[index] = start ? day.copyWith(start: minutes) : day.copyWith(end: minutes);
      });
    }
  }

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(employeeRepositoryProvider).saveSchedule(widget.employee.id, _days);
      if (mounted) {
        showMessage(context, 'Radno vrijeme za ${widget.employee.fullName} je sačuvano.');
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
    final invalid = _invalid;

    return ActionDialog(
      title: 'Radno vrijeme — ${widget.employee.fullName}',
      confirmLabel: 'Sačuvaj',
      width: 560,
      busy: _busy,
      error: _error ?? invalid,
      onConfirm: invalid == null ? _save : null,
      content: Column(
        children: [
          const Text(
            'Vremena su u satu servisa (UTC), kao i termini rezervacija.',
            style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
          ),
          const SizedBox(height: 8),
          for (var i = 0; i < _days.length; i++)
            Row(
              children: [
                SizedBox(width: 130, child: Text(_days[i].day.label)),
                Switch(
                  value: _days[i].isWorkingDay,
                  onChanged: (on) => setState(() => _days = [..._days]..[i] = _days[i].copyWith(isWorkingDay: on)),
                ),
                const SizedBox(width: 12),
                TextButton(
                  onPressed: _days[i].isWorkingDay ? () => _pick(i, start: true) : null,
                  child: Text(ScheduleDay.formatMinutes(_days[i].start)),
                ),
                const Text('–'),
                TextButton(
                  onPressed: _days[i].isWorkingDay ? () => _pick(i, start: false) : null,
                  child: Text(ScheduleDay.formatMinutes(_days[i].end)),
                ),
                if (!_days[i].isWorkingDay) const Text('Neradni dan', style: TextStyle(color: AppColors.textSecondary)),
              ],
            ),
        ],
      ),
    );
  }
}
