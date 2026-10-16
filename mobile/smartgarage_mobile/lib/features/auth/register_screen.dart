import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/form_state.dart';
import 'password_field.dart';

/// Self-registration always creates a customer; there is no role to choose.
class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> with ServerFormState {
  final _userName = TextEditingController();
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  @override
  void dispose() {
    for (final controller in [_userName, _firstName, _lastName, _email, _phone, _password, _confirm]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _register() => submit(
        () => ref.read(authProvider.notifier).register(
              userName: _userName.text.trim(),
              email: _email.text.trim(),
              firstName: _firstName.text.trim(),
              lastName: _lastName.text.trim(),
              phoneNumber: _phone.text.trim(),
              password: _password.text,
              confirmPassword: _confirm.text,
            ),
      );

  Widget _field(
    TextEditingController controller,
    String field,
    String label,
    IconData icon,
    String? Function(String?) validator, {
    TextInputType? keyboard,
  }) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: TextFormField(
          controller: controller,
          keyboardType: keyboard,
          textInputAction: TextInputAction.next,
          decoration: InputDecoration(labelText: label, prefixIcon: Icon(icon)),
          validator: validator,
          forceErrorText: serverError(field),
          onChanged: (_) => clearServerError(field),
        ),
      );

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Registracija')),
        body: SafeArea(
          child: Form(
            key: formKey,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _field(_firstName, 'firstName', 'Ime', Icons.badge_outlined, (v) => Validators.personName(v, 'Ime')),
                _field(_lastName, 'lastName', 'Prezime', Icons.badge_outlined, (v) => Validators.personName(v, 'Prezime')),
                _field(_userName, 'userName', 'Korisničko ime', Icons.person_outline, Validators.userName),
                _field(_email, 'email', 'E-mail adresa', Icons.alternate_email, Validators.email,
                    keyboard: TextInputType.emailAddress),
                _field(_phone, 'phoneNumber', 'Broj telefona', Icons.phone_outlined, Validators.phone,
                    keyboard: TextInputType.phone),
                PasswordField(
                  controller: _password,
                  label: 'Lozinka',
                  helper: 'Najmanje 8 znakova: veliko i malo slovo, cifra i specijalni znak.',
                  validator: Validators.password,
                  serverError: serverError('password'),
                  onChanged: () => clearServerError('password'),
                ),
                PasswordField(
                  controller: _confirm,
                  label: 'Potvrda lozinke',
                  validator: (v) => Validators.confirmPassword(v, _password.text),
                  serverError: serverError('confirmPassword'),
                  onChanged: () => clearServerError('confirmPassword'),
                ),
                FormErrorText(formError),
                BusyButton(label: 'Registruj se', busy: busy, onPressed: _register),
              ],
            ),
          ),
        ),
      );
}
