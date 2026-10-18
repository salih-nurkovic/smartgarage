import '../../core/formatting.dart';
import 'appointment.dart';
import 'common.dart';

/// A user account as the administrator manages it.
class ManagedUser {
  const ManagedUser({
    required this.id,
    required this.userName,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.phoneNumber,
    required this.isActive,
    required this.roles,
    required this.createdAtUtc,
    required this.lastLoginAtUtc,
  });

  factory ManagedUser.fromJson(Json json) => ManagedUser(
    id: json['id'] as int,
    userName: json['userName'] as String,
    email: json['email'] as String,
    firstName: json['firstName'] as String,
    lastName: json['lastName'] as String,
    phoneNumber: json['phoneNumber'] as String,
    isActive: json['isActive'] as bool,
    roles: toStrings(json['roles']),
    createdAtUtc: parseUtc(json['createdAtUtc'] as String),
    lastLoginAtUtc: parseUtcOrNull(json['lastLoginAtUtc']),
  );

  final int id;
  final String userName;
  final String email;
  final String firstName;
  final String lastName;
  final String phoneNumber;
  final bool isActive;
  final List<String> roles;
  final DateTime createdAtUtc;
  final DateTime? lastLoginAtUtc;

  String get fullName => '$firstName $lastName';

  String get role => roles.isEmpty ? '' : roles.first;
}

enum PaymentStatus {
  created('Kreirano'),
  requiresAction('Čeka potvrdu'),
  processing('U obradi'),
  succeeded('Uspješno'),
  failed('Neuspješno'),
  cancelled('Otkazano');

  const PaymentStatus(this.label);

  final String label;

  static PaymentStatus parse(Object? name) => byApiName(values, name);
}

class Refund {
  const Refund({required this.amount, required this.reason, required this.status, required this.requestedAtUtc});

  factory Refund.fromJson(Json json) => Refund(
    amount: toDouble(json['amount']),
    reason: json['reason'] as String,
    status: json['status'] as String,
    requestedAtUtc: parseUtc(json['requestedAtUtc'] as String),
  );

  final double amount;
  final String reason;
  final String status;
  final DateTime requestedAtUtc;

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
    required this.payerFullName,
    required this.serviceNames,
    required this.amount,
    required this.status,
    required this.createdAtUtc,
    required this.paidAtUtc,
    required this.failureReason,
    required this.amountCharged,
    required this.refundedAmount,
    required this.refundableAmount,
    required this.refunds,
  });

  factory Payment.fromJson(dynamic json) {
    final map = json as Json;
    return Payment(
      id: map['id'] as int,
      number: map['paymentNumber'] as String,
      appointmentId: map['appointmentId'] as int,
      appointmentNumber: map['appointmentNumber'] as String,
      payerFullName: map['payerFullName'] as String,
      serviceNames: toStrings(map['serviceNames']),
      amount: toDouble(map['amount']),
      status: PaymentStatus.parse(map['status']),
      createdAtUtc: parseUtc(map['createdAtUtc'] as String),
      paidAtUtc: parseUtcOrNull(map['paidAtUtc']),
      failureReason: map['failureReason'] as String?,
      amountCharged: toDouble(map['amountCharged']),
      refundedAmount: toDouble(map['refundedAmount']),
      refundableAmount: toDouble(map['refundableAmount']),
      refunds: toList(map['refunds'], Refund.fromJson),
    );
  }

  final int id;
  final String number;
  final int appointmentId;
  final String appointmentNumber;
  final String payerFullName;
  final List<String> serviceNames;
  final double amount;
  final PaymentStatus status;
  final DateTime createdAtUtc;
  final DateTime? paidAtUtc;
  final String? failureReason;
  final double amountCharged;
  final double refundedAmount;
  final double refundableAmount;
  final List<Refund> refunds;
}

class PaymentSummary {
  const PaymentSummary({
    required this.netRevenue,
    required this.succeeded,
    required this.inProgress,
    required this.failed,
  });

  factory PaymentSummary.fromJson(dynamic json) {
    final map = json as Json;
    return PaymentSummary(
      netRevenue: toDouble(map['netRevenue']),
      succeeded: map['succeeded'] as int,
      inProgress: map['inProgress'] as int,
      failed: map['failed'] as int,
    );
  }

  final double netRevenue;
  final int succeeded;
  final int inProgress;
  final int failed;
}

class Review {
  const Review({
    required this.id,
    required this.serviceName,
    required this.authorDisplayName,
    required this.rating,
    required this.comment,
    required this.createdAtUtc,
    required this.isVisible,
  });

  factory Review.fromJson(Json json) => Review(
    id: json['id'] as int,
    serviceName: json['serviceName'] as String,
    authorDisplayName: json['authorDisplayName'] as String,
    rating: json['rating'] as int,
    comment: json['comment'] as String?,
    createdAtUtc: parseUtc(json['createdAtUtc'] as String),
    isVisible: json['isVisible'] as bool,
  );

  final int id;
  final String serviceName;
  final String authorDisplayName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;
  final bool isVisible;
}

class ErrorLogItem {
  const ErrorLogItem({
    required this.id,
    required this.correlationId,
    required this.source,
    required this.message,
    required this.exceptionType,
    required this.httpMethod,
    required this.requestPath,
    required this.statusCode,
    required this.userName,
    required this.occurredAtUtc,
    required this.stackTrace,
  });

  factory ErrorLogItem.fromJson(dynamic json) {
    final map = json as Json;
    return ErrorLogItem(
      id: map['id'] as int,
      correlationId: map['correlationId'] as String,
      source: map['source'] as String,
      message: map['message'] as String,
      exceptionType: map['exceptionType'] as String?,
      httpMethod: map['httpMethod'] as String?,
      requestPath: map['requestPath'] as String?,
      statusCode: map['statusCode'] as int?,
      userName: map['userName'] as String?,
      occurredAtUtc: parseUtc(map['occurredAtUtc'] as String),
      stackTrace: map['stackTrace'] as String?,
    );
  }

  final int id;
  final String correlationId;
  final String source;
  final String message;
  final String? exceptionType;
  final String? httpMethod;
  final String? requestPath;
  final int? statusCode;
  final String? userName;
  final DateTime occurredAtUtc;

  /// Only present when one entry is fetched on its own.
  final String? stackTrace;
}

class Kpi {
  const Kpi(this.value, this.changePercent);

  factory Kpi.fromJson(Json json) => Kpi(toDouble(json['value']), toDoubleOrNull(json['changePercent']));

  final double value;
  final double? changePercent;
}

class MonthlyRevenue {
  const MonthlyRevenue(this.year, this.month, this.revenue);

  factory MonthlyRevenue.fromJson(Json json) =>
      MonthlyRevenue(json['year'] as int, json['month'] as int, toDouble(json['revenue']));

  final int year;
  final int month;
  final double revenue;
}

class ServiceShare {
  const ServiceShare(this.name, this.count, this.percent);

  factory ServiceShare.fromJson(Json json) =>
      ServiceShare(json['name'] as String, json['count'] as int, toDouble(json['percent']));

  final String name;
  final int count;
  final double percent;
}

class Dashboard {
  const Dashboard({
    required this.customers,
    required this.activeAppointments,
    required this.completedServices,
    required this.monthRevenue,
    required this.revenueByMonth,
    required this.serviceDistribution,
  });

  factory Dashboard.fromJson(dynamic json) {
    final map = json as Json;
    final revenue = map['monthRevenue'] as Json?;
    final byMonth = map['revenueByMonth'] as List?;
    return Dashboard(
      customers: Kpi.fromJson(map['customers'] as Json),
      activeAppointments: Kpi.fromJson(map['activeAppointments'] as Json),
      completedServices: Kpi.fromJson(map['completedServices'] as Json),
      monthRevenue: revenue == null ? null : Kpi.fromJson(revenue),
      revenueByMonth: byMonth == null ? null : toList(byMonth, MonthlyRevenue.fromJson),
      serviceDistribution: toList(map['serviceDistribution'], ServiceShare.fromJson),
    );
  }

  final Kpi customers;
  final Kpi activeAppointments;
  final Kpi completedServices;

  /// Null for employees: revenue is shown to administrators only.
  final Kpi? monthRevenue;
  final List<MonthlyRevenue>? revenueByMonth;
  final List<ServiceShare> serviceDistribution;
}
