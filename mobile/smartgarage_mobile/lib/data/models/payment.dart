import '../../core/formatting.dart';
import 'common.dart';

enum PaymentStatus {
  created('Kreirano'),
  requiresAction('Čeka potvrdu'),
  processing('U obradi'),
  succeeded('Uspješno'),
  failed('Neuspješno'),
  cancelled('Otkazano');

  const PaymentStatus(this.label);

  final String label;

  static PaymentStatus parse(Object? name) {
    final value = name as String;
    return values.byName('${value[0].toLowerCase()}${value.substring(1)}');
  }
}

class PaymentIntent {
  const PaymentIntent({required this.paymentId, required this.clientSecret, required this.publishableKey});

  factory PaymentIntent.fromJson(dynamic json) {
    final map = json as Json;
    return PaymentIntent(
      paymentId: map['paymentId'] as int,
      clientSecret: map['clientSecret'] as String,
      publishableKey: map['publishableKey'] as String,
    );
  }

  final int paymentId;
  final String clientSecret;
  final String publishableKey;
}

class Refund {
  const Refund({required this.amount, required this.reason, required this.requestedAtUtc, required this.status});

  factory Refund.fromJson(Json json) => Refund(
        amount: toDouble(json['amount']),
        reason: json['reason'] as String,
        requestedAtUtc: parseUtc(json['requestedAtUtc'] as String),
        status: json['status'] as String,
      );

  final double amount;
  final String reason;
  final DateTime requestedAtUtc;
  final String status;

  String get statusLabel => switch (status) {
        'Succeeded' => 'izvršen',
        'Pending' => 'u obradi',
        'Failed' => 'neuspješan',
        _ => 'otkazan',
      };
}

class Payment {
  const Payment({
    required this.id,
    required this.number,
    required this.appointmentId,
    required this.appointmentNumber,
    required this.serviceNames,
    required this.amount,
    required this.status,
    required this.createdAtUtc,
    required this.paidAtUtc,
    required this.failureReason,
    required this.refundedAmount,
    required this.refunds,
  });

  factory Payment.fromJson(dynamic json) {
    final map = json as Json;
    return Payment(
      id: map['id'] as int,
      number: map['paymentNumber'] as String,
      appointmentId: map['appointmentId'] as int,
      appointmentNumber: map['appointmentNumber'] as String,
      serviceNames: toStrings(map['serviceNames']),
      amount: toDouble(map['amount']),
      status: PaymentStatus.parse(map['status']),
      createdAtUtc: parseUtc(map['createdAtUtc'] as String),
      paidAtUtc: parseUtcOrNull(map['paidAtUtc']),
      failureReason: map['failureReason'] as String?,
      refundedAmount: toDouble(map['refundedAmount']),
      refunds: toList(map['refunds'], Refund.fromJson),
    );
  }

  final int id;
  final String number;
  final int appointmentId;
  final String appointmentNumber;
  final List<String> serviceNames;
  final double amount;
  final PaymentStatus status;
  final DateTime createdAtUtc;
  final DateTime? paidAtUtc;
  final String? failureReason;
  final double refundedAmount;
  final List<Refund> refunds;
}
