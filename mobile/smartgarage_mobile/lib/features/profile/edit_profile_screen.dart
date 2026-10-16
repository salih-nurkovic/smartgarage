import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';

/// Personal details only; the password has its own screen, so editing a name never asks
/// for one.
class EditProfileScreen extends ConsumerStatefulWidget {
  const EditProfileScreen({super.key});

  @override
  ConsumerState<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends ConsumerState<EditProfileScreen> with ServerFormState {
  late final _user = ref.read(authProvider).value!;
  late final _firstName = TextEditingController(text: _user.firstName);
  late final _lastName = TextEditingController(text: _user.lastName);
  late final _email = TextEditingController(text: _user.email);
  late final _phone = TextEditingController(text: _user.phoneNumber);

  @override
  void dispose() {
    for (final controller in [_firstName, _lastName, _email, _phone]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final saved = await submit(() async {
      final user = await ref.read(profileRepositoryProvider).update(
            email: _email.text.trim(),
            firstName: _firstName.text.trim(),
            lastName: _lastName.text.trim(),
            phoneNumber: _phone.text.trim(),
          );
      ref.read(authProvider.notifier).updateUser(user);
    });
    if (saved && mounted) {
      showMessage(context, 'Lični podaci su sačuvani.');
      Navigator.of(context).pop();
    }
  }

  Widget _field(TextEditingController controller, String field, String label, IconData icon,
          String? Function(String?) validator, {TextInputType? keyboard}) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: TextFormField(
          controller: controller,
          keyboardType: keyboard,
          decoration: InputDecoration(labelText: label, prefixIcon: Icon(icon)),
          validator: validator,
          forceErrorText: serverError(field),
          onChanged: (_) => clearServerError(field),
        ),
      );

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Uredi profil')),
        body: Form(
          key: formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _field(_firstName, 'firstName', 'Ime', Icons.badge_outlined, (v) => Validators.personName(v, 'Ime')),
              _field(_lastName, 'lastName', 'Prezime', Icons.badge_outlined, (v) => Validators.personName(v, 'Prezime')),
              _field(_email, 'email', 'E-mail adresa', Icons.alternate_email, Validators.email,
                  keyboard: TextInputType.emailAddress),
              _field(_phone, 'phoneNumber', 'Broj telefona', Icons.phone_outlined, Validators.phone,
                  keyboard: TextInputType.phone),
              FormErrorText(formError),
              BusyButton(label: 'Sačuvaj', busy: busy, onPressed: _save),
            ],
          ),
        ),
      );
}
