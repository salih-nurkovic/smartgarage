import 'package:flutter/material.dart';

import '../theme.dart';

/// Every irreversible action - deleting, paying, sending a booking - asks first.
Future<bool> confirm(
  BuildContext context, {
  required String title,
  required String message,
  required String confirmLabel,
  bool destructive = false,
}) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title),
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Odustani')),
        FilledButton(
          style: FilledButton.styleFrom(
            minimumSize: const Size(110, 44),
            backgroundColor: destructive ? AppColors.danger : AppColors.accent,
          ),
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(confirmLabel),
        ),
      ],
    ),
  );
  return result ?? false;
}

/// Asks for a required reason, validated under the field, then confirms.
Future<String?> askReason(
  BuildContext context, {
  required String title,
  required String message,
  required String fieldLabel,
  required String confirmLabel,
  required String? Function(String?) validator,
  bool destructive = false,
}) => showDialog<String>(
  context: context,
  builder: (context) => _ReasonDialog(
    title: title,
    message: message,
    fieldLabel: fieldLabel,
    confirmLabel: confirmLabel,
    validator: validator,
    destructive: destructive,
  ),
);

class _ReasonDialog extends StatefulWidget {
  const _ReasonDialog({
    required this.title,
    required this.message,
    required this.fieldLabel,
    required this.confirmLabel,
    required this.validator,
    required this.destructive,
  });

  final String title;
  final String message;
  final String fieldLabel;
  final String confirmLabel;
  final String? Function(String?) validator;
  final bool destructive;

  @override
  State<_ReasonDialog> createState() => _ReasonDialogState();
}

class _ReasonDialogState extends State<_ReasonDialog> {
  final _form = GlobalKey<FormState>();
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.title),
    content: Form(
      key: _form,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(widget.message),
          const SizedBox(height: 16),
          TextFormField(
            controller: _reason,
            autofocus: true,
            maxLines: 3,
            maxLength: 500,
            decoration: InputDecoration(labelText: widget.fieldLabel),
            validator: widget.validator,
          ),
        ],
      ),
    ),
    actions: [
      TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
      FilledButton(
        style: FilledButton.styleFrom(
          minimumSize: const Size(110, 44),
          backgroundColor: widget.destructive ? AppColors.danger : AppColors.accent,
        ),
        onPressed: () {
          if (_form.currentState!.validate()) {
            Navigator.of(context).pop(_reason.text.trim());
          }
        },
        child: Text(widget.confirmLabel),
      ),
    ],
  );
}

void showMessage(BuildContext context, String message, {bool error = false}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(content: Text(message), backgroundColor: error ? AppColors.danger : AppColors.surfaceRaised),
    );
}
