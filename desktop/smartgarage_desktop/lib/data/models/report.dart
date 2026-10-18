import '../../core/formatting.dart';
import 'admin.dart';
import 'appointment.dart';
import 'common.dart';

/// The days a report covers, both included, as the API takes them (`yyyy-MM-dd`).
class ReportRange {
  const ReportRange(this.from, this.to);

  final DateTime from;
  final DateTime to;

  @override
  bool operator ==(Object other) => other is ReportRange && other.from == from && other.to == to;

  @override
  int get hashCode => Object.hash(from, to);

  Map<String, dynamic> get query => {'from': _day(from), 'to': _day(to)};

  static String _day(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
}

enum ReportKind {
  revenue('revenue', 'Izvještaj o prihodima', 'prihodi'),
  appointments('appointments', 'Izvještaj o rezervacijama', 'rezervacije');

  const ReportKind(this.path, this.label, this.fileStem);

  final String path;
  final String label;
  final String fileStem;

  String fileName(ReportRange range) =>
      'izvjestaj-$fileStem-${range.query['from']}-${range.query['to']}.pdf';
}

class ReportPeriod {
  const ReportPeriod(this.label, this.revenue, this.appointments);

  factory ReportPeriod.fromJson(Json json) =>
      ReportPeriod(json['label'] as String, toDouble(json['revenue']), json['appointments'] as int);

  final String label;
  final double revenue;
  final int appointments;
}

class ReportShare {
  const ReportShare(this.name, this.count, this.amount, this.percent);

  factory ReportShare.fromJson(Json json) =>
      ReportShare(json['name'] as String, json['count'] as int, toDouble(json['amount']), toDouble(json['percent']));

  final String name;
  final int count;
  final double amount;
  final double percent;
}

class ReportRefund {
  const ReportRefund(this.paymentNumber, this.completedAtUtc, this.amount, this.reason, this.requestedBy);

  factory ReportRefund.fromJson(Json json) => ReportRefund(
    json['paymentNumber'] as String,
    parseUtc(json['completedAtUtc'] as String),
    toDouble(json['amount']),
    json['reason'] as String,
    json['requestedBy'] as String,
  );

  final String paymentNumber;
  final DateTime completedAtUtc;
  final double amount;
  final String reason;
  final String requestedBy;
}

class RevenueReport {
  const RevenueReport({
    required this.grossRevenue,
    required this.refundedAmount,
    required this.netRevenue,
    required this.paymentCount,
    required this.averageTransaction,
    required this.byPeriod,
    required this.byCategory,
    required this.topServices,
    required this.refunds,
  });

  factory RevenueReport.fromJson(dynamic json) {
    final map = json as Json;
    return RevenueReport(
      grossRevenue: Kpi.fromJson(map['grossRevenue'] as Json),
      refundedAmount: toDouble(map['refundedAmount']),
      netRevenue: Kpi.fromJson(map['netRevenue'] as Json),
      paymentCount: map['paymentCount'] as int,
      averageTransaction: Kpi.fromJson(map['averageTransaction'] as Json),
      byPeriod: toList(map['byPeriod'], ReportPeriod.fromJson),
      byCategory: toList(map['byCategory'], ReportShare.fromJson),
      topServices: toList(map['topServices'], ReportShare.fromJson),
      refunds: toList(map['refunds'], ReportRefund.fromJson),
    );
  }

  final Kpi grossRevenue;
  final double refundedAmount;
  final Kpi netRevenue;
  final int paymentCount;
  final Kpi averageTransaction;
  final List<ReportPeriod> byPeriod;
  final List<ReportShare> byCategory;
  final List<ReportShare> topServices;
  final List<ReportRefund> refunds;
}

class StatusCount {
  const StatusCount(this.status, this.count);

  factory StatusCount.fromJson(Json json) => StatusCount(AppointmentStatus.parse(json['status']), json['count'] as int);

  final AppointmentStatus status;
  final int count;
}

class EmployeeFigures {
  const EmployeeFigures(this.name, this.total, this.completed, this.completionRate);

  factory EmployeeFigures.fromJson(Json json) => EmployeeFigures(
    json['name'] as String,
    json['total'] as int,
    json['completed'] as int,
    toDouble(json['completionRate']),
  );

  final String name;
  final int total;
  final int completed;
  final double completionRate;
}

class ReasonCount {
  const ReasonCount(this.reason, this.count);

  factory ReasonCount.fromJson(Json json) => ReasonCount(json['reason'] as String, json['count'] as int);

  final String reason;
  final int count;
}

class AppointmentReport {
  const AppointmentReport({
    required this.total,
    required this.completed,
    required this.cancelled,
    required this.completionRate,
    required this.averageDurationMinutes,
    required this.averagePrice,
    required this.averageRating,
    required this.byPeriod,
    required this.byStatus,
    required this.byCategory,
    required this.byEmployee,
    required this.cancellationReasons,
  });

  factory AppointmentReport.fromJson(dynamic json) {
    final map = json as Json;
    final rating = map['averageRating'] as Json?;
    return AppointmentReport(
      total: Kpi.fromJson(map['total'] as Json),
      completed: map['completed'] as int,
      cancelled: map['cancelled'] as int,
      completionRate: toDouble(map['completionRate']),
      averageDurationMinutes: toDouble(map['averageDurationMinutes']).round(),
      averagePrice: Kpi.fromJson(map['averagePrice'] as Json),
      averageRating: rating == null ? null : Kpi.fromJson(rating),
      byPeriod: toList(map['byPeriod'], ReportPeriod.fromJson),
      byStatus: toList(map['byStatus'], StatusCount.fromJson),
      byCategory: toList(map['byCategory'], ReportShare.fromJson),
      byEmployee: toList(map['byEmployee'], EmployeeFigures.fromJson),
      cancellationReasons: toList(map['cancellationReasons'], ReasonCount.fromJson),
    );
  }

  final Kpi total;
  final int completed;
  final int cancelled;
  final double completionRate;
  final int averageDurationMinutes;
  final Kpi averagePrice;

  /// 1 to 5, or null when nobody wrote a review in the range.
  final Kpi? averageRating;
  final List<ReportPeriod> byPeriod;
  final List<StatusCount> byStatus;
  final List<ReportShare> byCategory;
  final List<EmployeeFigures> byEmployee;
  final List<ReasonCount> cancellationReasons;
}
