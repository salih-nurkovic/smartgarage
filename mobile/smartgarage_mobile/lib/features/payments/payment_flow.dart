import 'package:flutter/material.dart';
import 'package:flutter_stripe/flutter_stripe.dart';

import '../../core/api/api_exception.dart';
import '../../core/config.dart';
import '../../data/models/payment.dart';
import '../../data/repositories.dart';

enum PaymentOutcome { succeeded, failed, cancelled, pending }

typedef PaymentResult = (PaymentOutcome outcome, String message);

/// Pays for a completed appointment inside the app, through Stripe's own payment sheet.
///
/// The app sends only the appointment; the server sets the amount. Whatever the sheet reports,
/// the app then asks the server, which checks with Stripe - the phone never declares a payment
/// successful.
class PaymentFlow {
  const PaymentFlow(this._payments);

  static const _confirmationAttempts = 10;
  static const _confirmationDelay = Duration(seconds: 2);

  final PaymentRepository _payments;

  Future<PaymentResult> pay(int appointmentId) async {
    final intent = await _payments.createIntent(appointmentId);

    try {
      Stripe.publishableKey = intent.publishableKey;
      await Stripe.instance.applySettings();
      await Stripe.instance.initPaymentSheet(
        paymentSheetParameters: SetupPaymentSheetParameters(
          paymentIntentClientSecret: intent.clientSecret,
          merchantDisplayName: AppConfig.merchantDisplayName,
          style: ThemeMode.dark,
        ),
      );
    } on Exception catch (error) {
      debugPrint('Stripe payment sheet could not start: $error');
      throw ApiException('Plaćanje trenutno nije moguće pokrenuti. Pokušajte ponovo za nekoliko trenutaka.');
    }

    try {
      await Stripe.instance.presentPaymentSheet();
    } on StripeException catch (error) {
      if (error.error.code == FailureCode.Canceled) {
        return (PaymentOutcome.cancelled, 'Plaćanje je prekinuto. Ništa nije naplaćeno.');
      }
      return (PaymentOutcome.failed, error.error.localizedMessage ?? 'Plaćanje nije uspjelo. Pokušajte ponovo.');
    }

    return _awaitServerConfirmation(intent.paymentId);
  }

  // Stripe may take a moment to settle; the server is asked a few times before giving up.
  Future<PaymentResult> _awaitServerConfirmation(int paymentId) async {
    for (var attempt = 0; attempt < _confirmationAttempts; attempt++) {
      final payment = await _payments.get(paymentId);

      switch (payment.status) {
        case PaymentStatus.succeeded:
          return (PaymentOutcome.succeeded, 'Plaćanje ${payment.number} je uspješno. Hvala!');
        case PaymentStatus.failed || PaymentStatus.cancelled:
          return (PaymentOutcome.failed, payment.failureReason ?? 'Plaćanje nije uspjelo. Pokušajte ponovo.');
        case _:
          await Future<void>.delayed(_confirmationDelay);
      }
    }
    return (
      PaymentOutcome.pending,
      'Plaćanje se još obrađuje. Status će se ažurirati čim ga Stripe potvrdi.',
    );
  }
}
