import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/form_state.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> with ServerFormState {
  final _userName = TextEditingController();
  final _password = TextEditingController();
  var _hidePassword = true;

  @override
  void dispose() {
    _userName.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _login() => submit(() => ref.read(authProvider.notifier).login(_userName.text.trim(), _password.text));

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Center(
      child: SizedBox(
        width: 420,
        child: Card(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Form(
              key: formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(Icons.directions_car_filled_outlined, size: 56, color: AppColors.accent),
                  const SizedBox(height: 8),
                  const Text(
                    'Smart Garage',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 28, fontWeight: FontWeight.w800),
                  ),
                  const Text(
                    'Administracija servisa',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                  const SizedBox(height: 28),
                  TextFormField(
                    controller: _userName,
                    autofocus: true,
                    textInputAction: TextInputAction.next,
                    decoration: const InputDecoration(
                      labelText: 'Korisničko ime',
                      prefixIcon: Icon(Icons.person_outline),
                    ),
                    validator: (v) => Validators.required(v, 'Korisničko ime'),
                    forceErrorText: serverError('userName'),
                    onChanged: (_) => clearServerError('userName'),
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _password,
                    obscureText: _hidePassword,
                    onFieldSubmitted: (_) => _login(),
                    decoration: InputDecoration(
                      labelText: 'Lozinka',
                      prefixIcon: const Icon(Icons.lock_outline),
                      suffixIcon: IconButton(
                        tooltip: _hidePassword ? 'Prikaži lozinku' : 'Sakrij lozinku',
                        icon: Icon(_hidePassword ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                        onPressed: () => setState(() => _hidePassword = !_hidePassword),
                      ),
                    ),
                    validator: (v) => Validators.required(v, 'Lozinka'),
                    forceErrorText: serverError('password'),
                    onChanged: (_) => clearServerError('password'),
                  ),
                  const SizedBox(height: 24),
                  FormErrorText(formError),
                  BusyButton(label: 'Prijavi se', busy: busy, onPressed: _login),
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}
