import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/admin.dart';
import '../../data/models/user.dart';
import '../appointments/appointment_dialogs.dart';

class UsersScreen extends ConsumerStatefulWidget {
  const UsersScreen({super.key});

  @override
  ConsumerState<UsersScreen> createState() => _UsersScreenState();
}

class _UsersScreenState extends ConsumerState<UsersScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  String? _role;
  bool? _active;
  var _version = 0;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _edit([ManagedUser? user]) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => _UserFormDialog(user: user),
    );
    if (saved ?? false) {
      setState(() => _version++);
    }
  }

  Future<void> _toggle(ManagedUser user) async {
    String? reason;
    if (user.isActive) {
      reason = await askReason(
        context,
        title: 'Deaktivirati nalog?',
        message: '${user.fullName} (${user.userName}) se više neće moći prijaviti, a otvorene sesije se zatvaraju.',
        fieldLabel: 'Razlog deaktivacije',
        confirmLabel: 'Deaktiviraj',
        validator: (v) => Validators.reason(v, 'Razlog deaktivacije'),
        destructive: true,
      );
      if (reason == null) {
        return;
      }
    } else {
      final ok = await confirm(
        context,
        title: 'Aktivirati nalog?',
        message: '${user.fullName} (${user.userName}) će se ponovo moći prijaviti.',
        confirmLabel: 'Aktiviraj',
      );
      if (!ok) {
        return;
      }
    }
    if (!mounted) {
      return;
    }
    try {
      await ref.read(userRepositoryProvider).setStatus(user.id, isActive: !user.isActive, reason: reason);
      if (mounted) {
        showMessage(
          context,
          user.isActive ? 'Nalog ${user.userName} je deaktiviran.' : 'Nalog ${user.userName} je aktiviran.',
        );
        setState(() => _version++);
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(userRepositoryProvider);
    final me = ref.watch(authProvider).value;

    return ScreenScaffold(
      title: 'Korisnici',
      subtitle: 'Nalozi kupaca, zaposlenika i administratora',
      actions: [
        FilledButton.icon(
          onPressed: _edit,
          icon: const Icon(Icons.person_add_alt),
          label: const Text('Dodaj korisnika'),
        ),
      ],
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      decoration: const InputDecoration(
                        hintText: 'Pretraži po imenu, korisničkom imenu ili e-mailu...',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: (value) {
                        _debounce?.cancel();
                        _debounce = Timer(const Duration(milliseconds: 400), () => setState(() => _term = value));
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 190,
                    child: DropdownButtonFormField<String?>(
                      initialValue: _role,
                      decoration: const InputDecoration(labelText: 'Uloga'),
                      items: [
                        const DropdownMenuItem(child: Text('Sve uloge')),
                        for (final role in [Roles.administrator, Roles.employee, Roles.customer])
                          DropdownMenuItem(value: role, child: Text(Roles.label(role))),
                      ],
                      onChanged: (v) => setState(() => _role = v),
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 170,
                    child: DropdownButtonFormField<bool?>(
                      initialValue: _active,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: const [
                        DropdownMenuItem(child: Text('Svi')),
                        DropdownMenuItem(value: true, child: Text('Aktivni')),
                        DropdownMenuItem(value: false, child: Text('Neaktivni')),
                      ],
                      onChanged: (v) => setState(() => _active = v),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<ManagedUser>(
              reloadKey: (_term, _role, _active, _version),
              fetch: (page) => repository.list(search: _term, role: _role, isActive: _active, page: page),
              emptyMessage: 'Nema korisnika koji odgovaraju filterima.',
              builder: (context, items, _) => SimpleTable<ManagedUser>(
                items: items,
                onTap: _edit,
                columns: [
                  TableColumn(
                    'Ime i prezime',
                    (u) => Text(u.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
                    flex: 2,
                  ),
                  TableColumn('Korisničko ime', (u) => Text(u.userName)),
                  TableColumn('E-mail', (u) => Text(u.email, overflow: TextOverflow.ellipsis), flex: 2),
                  TableColumn('Telefon', (u) => Text(u.phoneNumber)),
                  TableColumn('Uloga', (u) => Text(Roles.label(u.role))),
                  TableColumn(
                    'Zadnja prijava',
                    (u) => Text(u.lastLoginAtUtc == null ? '—' : Fmt.dateTime(u.lastLoginAtUtc!)),
                  ),
                  TableColumn(
                    'Status',
                    (u) => Row(
                      children: [
                        Pill(
                          label: u.isActive ? 'Aktivan' : 'Neaktivan',
                          color: u.isActive ? AppColors.success : AppColors.textSecondary,
                        ),
                        const Spacer(),
                        GuardedIcon(
                          tooltip: u.isActive ? 'Deaktiviraj' : 'Aktiviraj',
                          icon: u.isActive ? Icons.block : Icons.check_circle_outline,
                          onPressed: me?.userName == u.userName ? null : () => _toggle(u),
                          disabledReason: 'Vlastiti nalog ne možete deaktivirati.',
                        ),
                      ],
                    ),
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

/// An icon button that explains itself when disabled.
class GuardedIcon extends StatelessWidget {
  const GuardedIcon({
    required this.tooltip,
    required this.icon,
    required this.onPressed,
    required this.disabledReason,
    super.key,
  });

  final String tooltip;
  final IconData icon;
  final VoidCallback? onPressed;
  final String disabledReason;

  @override
  Widget build(BuildContext context) => Tooltip(
    message: onPressed == null ? disabledReason : tooltip,
    child: IconButton(onPressed: onPressed, icon: Icon(icon)),
  );
}

/// New account or edit. Employees are created on the Employees screen, together with their work
/// profile, so that role is not offered here.
class _UserFormDialog extends ConsumerStatefulWidget {
  const _UserFormDialog({this.user});

  final ManagedUser? user;

  @override
  ConsumerState<_UserFormDialog> createState() => _UserFormDialogState();
}

class _UserFormDialogState extends ConsumerState<_UserFormDialog> with ServerFormState {
  late final ManagedUser? _u = widget.user;
  late final _userName = TextEditingController(text: _u?.userName);
  late final _firstName = TextEditingController(text: _u?.firstName);
  late final _lastName = TextEditingController(text: _u?.lastName);
  late final _email = TextEditingController(text: _u?.email);
  late final _phone = TextEditingController(text: _u?.phoneNumber);
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  late String _role = _u?.role ?? Roles.customer;
  var _changePassword = false;

  bool get _editing => _u != null;

  @override
  void dispose() {
    for (final c in [_userName, _firstName, _lastName, _email, _phone, _password, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final repository = ref.read(userRepositoryProvider);
    final saved = await submit(() async {
      if (_editing) {
        await repository.update(_u!.id, {
          'email': _email.text.trim(),
          'firstName': _firstName.text.trim(),
          'lastName': _lastName.text.trim(),
          'phoneNumber': _phone.text.trim(),
          'role': _role,
          // Empty means "leave the password as it is"; an administrator never needs the old one.
          'newPassword': _changePassword ? _password.text : null,
          'confirmNewPassword': _changePassword ? _confirm.text : null,
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
          'role': _role,
        });
      }
    });
    if (saved && mounted) {
      showMessage(context, _editing ? 'Izmjene korisnika su sačuvane.' : 'Korisnik ${_userName.text.trim()} je dodan.');
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final roles = [Roles.customer, Roles.administrator, if (_u?.role == Roles.employee) Roles.employee];
    final showPassword = !_editing || _changePassword;

    return ActionDialog(
      title: _editing ? 'Uredi korisnika' : 'Novi korisnik',
      confirmLabel: 'Sačuvaj',
      width: 600,
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
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: DropdownButtonFormField<String>(
                initialValue: _role,
                decoration: InputDecoration(
                  labelText: 'Uloga',
                  helperText: _role == Roles.employee
                      ? 'Zaposlenici se uređuju na ekranu Zaposlenici.'
                      : 'Zaposlenike dodajte na ekranu Zaposlenici, zajedno sa radnim profilom.',
                ),
                items: [for (final r in roles) DropdownMenuItem(value: r, child: Text(Roles.label(r)))],
                onChanged: _role == Roles.employee ? null : (v) => setState(() => _role = v ?? _role),
                forceErrorText: serverError('role'),
              ),
            ),
            if (_editing)
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Postavi novu lozinku'),
                subtitle: const Text('Ostavite neoznačeno da lozinka ostane ista.'),
                value: _changePassword,
                onChanged: (v) => setState(() => _changePassword = v ?? false),
              ),
            if (showPassword) ...[
              _field(
                _password,
                _editing ? 'newPassword' : 'password',
                _editing ? 'Nova lozinka' : 'Lozinka',
                Validators.password,
                obscure: true,
              ),
              _field(
                _confirm,
                _editing ? 'confirmNewPassword' : 'confirmPassword',
                'Potvrda lozinke',
                (v) => Validators.confirmPassword(v, _password.text),
                obscure: true,
              ),
            ],
          ],
        ),
      ),
    );
  }

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
