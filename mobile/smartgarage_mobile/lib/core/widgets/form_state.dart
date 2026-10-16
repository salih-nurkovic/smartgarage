import 'package:flutter/widgets.dart';

import '../api/api_exception.dart';

/// Busy flag and server errors for a form screen.
///
/// A field error from the server is shown under its own input (through `forceErrorText`) and
/// disappears as soon as the user edits that field; anything else is shown above the submit
/// button. The server's wording is passed through as it is.
mixin ServerFormState<T extends StatefulWidget> on State<T> {
  final formKey = GlobalKey<FormState>();

  bool busy = false;
  String? formError;
  Map<String, String> fieldErrors = const {};

  String? serverError(String field) => fieldErrors[field];

  void clearServerError(String field) {
    if (fieldErrors.containsKey(field)) {
      setState(() => fieldErrors = Map.of(fieldErrors)..remove(field));
    }
  }

  /// Validates, then runs [action] with the busy flag set and the server's errors captured.
  /// Returns whether it succeeded.
  Future<bool> submit(Future<void> Function() action) async {
    setState(() {
      formError = null;
      fieldErrors = const {};
    });
    if (!formKey.currentState!.validate()) {
      return false;
    }

    setState(() => busy = true);
    try {
      await action();
      return true;
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          if (error is ApiException) {
            fieldErrors = error.fieldErrors;
            formError = error.fieldErrors.isEmpty ? error.message : null;
          } else {
            formError = errorMessage(error);
          }
        });
      }
      return false;
    } finally {
      if (mounted) {
        setState(() => busy = false);
      }
    }
  }
}
