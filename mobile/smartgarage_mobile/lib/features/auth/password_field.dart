import 'package:flutter/material.dart';

class PasswordField extends StatefulWidget {
  const PasswordField({
    required this.controller,
    required this.label,
    required this.validator,
    required this.onChanged,
    this.serverError,
    this.helper,
    super.key,
  });

  final TextEditingController controller;
  final String label;
  final String? helper;
  final String? Function(String?) validator;
  final String? serverError;
  final VoidCallback onChanged;

  @override
  State<PasswordField> createState() => _PasswordFieldState();
}

class _PasswordFieldState extends State<PasswordField> {
  var _hidden = true;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: TextFormField(
          controller: widget.controller,
          obscureText: _hidden,
          decoration: InputDecoration(
            labelText: widget.label,
            helperText: widget.helper,
            helperMaxLines: 2,
            prefixIcon: const Icon(Icons.lock_outline),
            suffixIcon: IconButton(
              tooltip: _hidden ? 'Prikaži lozinku' : 'Sakrij lozinku',
              icon: Icon(_hidden ? Icons.visibility_outlined : Icons.visibility_off_outlined),
              onPressed: () => setState(() => _hidden = !_hidden),
            ),
          ),
          validator: widget.validator,
          forceErrorText: widget.serverError,
          onChanged: (_) => widget.onChanged(),
        ),
      );
}
