import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../auth/password_field.dart';

/// Changing one's own password asks for the current one first.
class ChangePasswordScreen extends ConsumerStatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  ConsumerState<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends ConsumerState<ChangePasswordScreen> with ServerFormState {
  final _current = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  @override
  void dispose() {
    for (final controller in [_current, _password, _confirm]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final saved = await submit(
      () => ref.read(authRepositoryProvider).changePassword(
            currentPassword: _current.text,
            newPassword: _password.text,
            confirmNewPassword: _confirm.text,
          ),
    );
    if (saved && mounted) {
      showMessage(context, 'Lozinka je promijenjena. Ostali uređaji su odjavljeni.');
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Promjena lozinke')),
        body: Form(
          key: formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              PasswordField(
                controller: _current,
                label: 'Trenutna lozinka',
                validator: (v) => Validators.required(v, 'Trenutna lozinka'),
                serverError: serverError('currentPassword'),
                onChanged: () => clearServerError('currentPassword'),
              ),
              PasswordField(
                controller: _password,
                label: 'Nova lozinka',
                helper: 'Najmanje 8 znakova: veliko i malo slovo, cifra i specijalni znak.',
                validator: (v) =>
                    Validators.password(v) ??
                    (v == _current.text ? 'Nova lozinka mora biti različita od trenutne.' : null),
                serverError: serverError('newPassword'),
                onChanged: () => clearServerError('newPassword'),
              ),
              PasswordField(
                controller: _confirm,
                label: 'Potvrda nove lozinke',
                validator: (v) => Validators.confirmPassword(v, _password.text),
                serverError: serverError('confirmNewPassword'),
                onChanged: () => clearServerError('confirmNewPassword'),
              ),
              FormErrorText(formError),
              BusyButton(label: 'Promijeni lozinku', busy: busy, onPressed: _save),
            ],
          ),
        ),
      );
}
