import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import 'password_field.dart';

/// Password reset in two steps: ask for a code by e-mail, then enter it with the new password.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> with ServerFormState {
  final _account = TextEditingController();
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  var _codeSent = false;

  @override
  void dispose() {
    for (final controller in [_account, _code, _password, _confirm]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _requestCode() async {
    final sent = await submit(() => ref.read(authRepositoryProvider).forgotPassword(_account.text.trim()));
    if (sent && mounted) {
      setState(() => _codeSent = true);
    }
  }

  Future<void> _reset() async {
    final done = await submit(
      () => ref.read(authRepositoryProvider).resetPassword(
            userNameOrEmail: _account.text.trim(),
            code: _code.text.trim(),
            newPassword: _password.text,
            confirmNewPassword: _confirm.text,
          ),
    );
    if (done && mounted) {
      showMessage(context, 'Lozinka je promijenjena. Prijavite se novom lozinkom.');
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Reset lozinke')),
        body: SafeArea(
          child: Form(
            key: formKey,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  _codeSent
                      ? 'Ako nalog postoji, poslali smo kod na njegovu e-mail adresu. Unesite kod i novu lozinku.'
                      : 'Unesite korisničko ime ili e-mail adresu. Poslat ćemo vam kod za postavljanje nove lozinke.',
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 20),
                TextFormField(
                  controller: _account,
                  enabled: !_codeSent,
                  decoration: const InputDecoration(
                    labelText: 'Korisničko ime ili e-mail',
                    prefixIcon: Icon(Icons.person_outline),
                  ),
                  validator: (v) => Validators.required(v, 'Korisničko ime ili e-mail'),
                  forceErrorText: serverError('userNameOrEmail'),
                  onChanged: (_) => clearServerError('userNameOrEmail'),
                ),
                const SizedBox(height: 16),
                if (_codeSent) ...[
                  TextFormField(
                    controller: _code,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(labelText: 'Kod iz e-maila', prefixIcon: Icon(Icons.pin_outlined)),
                    validator: Validators.resetCode,
                    forceErrorText: serverError('code'),
                    onChanged: (_) => clearServerError('code'),
                  ),
                  const SizedBox(height: 16),
                  PasswordField(
                    controller: _password,
                    label: 'Nova lozinka',
                    helper: 'Najmanje 8 znakova: veliko i malo slovo, cifra i specijalni znak.',
                    validator: Validators.password,
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
                ],
                FormErrorText(formError),
                BusyButton(
                  label: _codeSent ? 'Postavi novu lozinku' : 'Pošalji kod',
                  busy: busy,
                  onPressed: _codeSent ? _reset : _requestCode,
                ),
                if (_codeSent)
                  TextButton(
                    onPressed: busy ? null : () => setState(() => _codeSent = false),
                    child: const Text('Pošalji kod ponovo'),
                  ),
              ],
            ),
          ),
        ),
      );
}
