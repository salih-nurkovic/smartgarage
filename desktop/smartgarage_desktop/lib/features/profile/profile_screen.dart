import 'dart:typed_data';

import 'package:file_selector/file_selector.dart';
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
import '../../data/models/user.dart';

final _avatarProvider = FutureProvider.autoDispose.family<Uint8List, User>(
  (ref, user) => ref.watch(profileRepositoryProvider).image(),
);

/// The signed-in staff member's own details, picture and password.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).value;
    if (user == null) {
      return const SizedBox.shrink();
    }

    return ScreenScaffold(
      title: 'Moj profil',
      subtitle: 'Lični podaci, slika i lozinka',
      body: ListView(
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: _DetailsForm(user: user)),
              const SizedBox(width: 16),
              const Expanded(child: _PasswordForm()),
            ],
          ),
        ],
      ),
    );
  }
}

class _DetailsForm extends ConsumerStatefulWidget {
  const _DetailsForm({required this.user});

  final User user;

  @override
  ConsumerState<_DetailsForm> createState() => _DetailsFormState();
}

class _DetailsFormState extends ConsumerState<_DetailsForm> with ServerFormState {
  late final _firstName = TextEditingController(text: widget.user.firstName);
  late final _lastName = TextEditingController(text: widget.user.lastName);
  late final _email = TextEditingController(text: widget.user.email);
  late final _phone = TextEditingController(text: widget.user.phoneNumber);
  var _uploading = false;

  @override
  void dispose() {
    for (final c in [_firstName, _lastName, _email, _phone]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _changePhoto() async {
    final file = await openFile(
      acceptedTypeGroups: const [
        XTypeGroup(label: 'Slike', extensions: ['jpg', 'jpeg', 'png', 'webp']),
      ],
    );
    if (file == null) {
      return;
    }
    setState(() => _uploading = true);
    try {
      final user = await ref.read(profileRepositoryProvider).uploadImage(await file.readAsBytes(), file.name);
      ref.read(authProvider.notifier).updateUser(user);
      if (mounted) {
        showMessage(context, 'Profilna slika je promijenjena.');
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    } finally {
      if (mounted) {
        setState(() => _uploading = false);
      }
    }
  }

  Future<void> _save() async {
    final saved = await submit(() async {
      final user = await ref
          .read(profileRepositoryProvider)
          .update(
            email: _email.text.trim(),
            firstName: _firstName.text.trim(),
            lastName: _lastName.text.trim(),
            phoneNumber: _phone.text.trim(),
          );
      ref.read(authProvider.notifier).updateUser(user);
    });
    if (saved && mounted) {
      showMessage(context, 'Lični podaci su sačuvani.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = widget.user;
    final initials = CircleAvatar(
      radius: 44,
      backgroundColor: AppColors.accentSoft,
      child: Text(
        user.initials,
        style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w800, color: AppColors.accent),
      ),
    );

    return SectionCard(
      title: 'Lični podaci',
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                user.hasProfileImage
                    ? ref
                          .watch(_avatarProvider(user))
                          .maybeWhen(
                            data: (bytes) => CircleAvatar(radius: 44, backgroundImage: MemoryImage(bytes)),
                            orElse: () => initials,
                          )
                    : initials,
                const SizedBox(width: 16),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('@${user.userName}', style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      user.roles.map(Roles.label).join(', '),
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                    Text(
                      'Član od ${Fmt.date(user.createdAtUtc)}',
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                    const SizedBox(height: 8),
                    OutlinedButton.icon(
                      onPressed: _uploading ? null : _changePhoto,
                      icon: const Icon(Icons.photo_camera_outlined, size: 18),
                      label: Text(_uploading ? 'Šaljem...' : 'Promijeni sliku'),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 20),
            _field(_firstName, 'firstName', 'Ime', (v) => Validators.personName(v, 'Ime')),
            _field(_lastName, 'lastName', 'Prezime', (v) => Validators.personName(v, 'Prezime')),
            _field(_email, 'email', 'E-mail adresa', Validators.email),
            _field(_phone, 'phoneNumber', 'Broj telefona', Validators.phone),
            FormErrorText(formError),
            Align(
              alignment: Alignment.centerRight,
              child: SizedBox(
                width: 180,
                child: BusyButton(label: 'Sačuvaj', busy: busy, onPressed: _save),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _field(TextEditingController c, String field, String label, String? Function(String?) validator) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: TextFormField(
      controller: c,
      decoration: InputDecoration(labelText: label),
      validator: validator,
      forceErrorText: serverError(field),
      onChanged: (_) => clearServerError(field),
    ),
  );
}

/// Changing one's own password asks for the current one.
class _PasswordForm extends ConsumerStatefulWidget {
  const _PasswordForm();

  @override
  ConsumerState<_PasswordForm> createState() => _PasswordFormState();
}

class _PasswordFormState extends ConsumerState<_PasswordForm> with ServerFormState {
  final _current = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  @override
  void dispose() {
    for (final c in [_current, _password, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final saved = await submit(
      () => ref
          .read(authRepositoryProvider)
          .changePassword(
            currentPassword: _current.text,
            newPassword: _password.text,
            confirmNewPassword: _confirm.text,
          ),
    );
    if (saved && mounted) {
      for (final c in [_current, _password, _confirm]) {
        c.clear();
      }
      showMessage(context, 'Lozinka je promijenjena. Ostale sesije su odjavljene.');
    }
  }

  Widget _field(
    TextEditingController c,
    String field,
    String label,
    String? Function(String?) validator, {
    String? helper,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: TextFormField(
      controller: c,
      obscureText: true,
      decoration: InputDecoration(labelText: label, helperText: helper),
      validator: validator,
      forceErrorText: serverError(field),
      onChanged: (_) => clearServerError(field),
    ),
  );

  @override
  Widget build(BuildContext context) => SectionCard(
    title: 'Promjena lozinke',
    child: Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _field(_current, 'currentPassword', 'Trenutna lozinka', (v) => Validators.required(v, 'Trenutna lozinka')),
          _field(
            _password,
            'newPassword',
            'Nova lozinka',
            (v) =>
                Validators.password(v) ?? (v == _current.text ? 'Nova lozinka mora biti različita od trenutne.' : null),
            helper: 'Najmanje 8 znakova: veliko i malo slovo, cifra i specijalni znak.',
          ),
          _field(
            _confirm,
            'confirmNewPassword',
            'Potvrda nove lozinke',
            (v) => Validators.confirmPassword(v, _password.text),
          ),
          FormErrorText(formError),
          Align(
            alignment: Alignment.centerRight,
            child: SizedBox(
              width: 220,
              child: BusyButton(label: 'Promijeni lozinku', busy: busy, onPressed: _save),
            ),
          ),
        ],
      ),
    ),
  );
}
