import '../../core/formatting.dart';
import 'common.dart';

/// The API sends enums by name.
T _byName<T extends Enum>(List<T> values, Object? name) => values.byName(name as String);

enum AppointmentStatus {
  pending('Na čekanju'),
  confirmed('Potvrđeno'),
  inProgress('U toku'),
  completed('Završeno'),
  cancelled('Otkazano');

  const AppointmentStatus(this.label);

  final String label;

  static AppointmentStatus parse(Object? name) =>
      _byName(values, '${(name as String)[0].toLowerCase()}${name.substring(1)}');

  bool get isActive => this == pending || this == confirmed || this == inProgress;

  /// The query-string value the API expects.
  String get apiName => '${name[0].toUpperCase()}${name.substring(1)}';
}

enum PaymentState {
  unpaid('Nije plaćeno'),
  pendingPayment('Plaćanje u obradi'),
  paid('Plaćeno'),
  partiallyRefunded('Djelimično vraćeno'),
  refunded('Vraćeno');

  const PaymentState(this.label);

  final String label;

  static PaymentState parse(Object? name) =>
      _byName(values, '${(name as String)[0].toLowerCase()}${name.substring(1)}');

  bool get isPaid => this == paid || this == partiallyRefunded || this == refunded;
}

abstract final class AppointmentActions {
  static const cancel = 'Cancel';
  static const reschedule = 'Reschedule';
  static const leaveReview = 'LeaveReview';
}

class AppointmentListItem {
  const AppointmentListItem({
    required this.id,
    required this.number,
    required this.startUtc,
    required this.endUtc,
    required this.status,
    required this.paymentStatus,
    required this.totalPrice,
    required this.vehicleId,
    required this.vehicleDisplay,
    required this.assignedEmployeeName,
    required this.serviceNames,
  });

  factory AppointmentListItem.fromJson(Json json) => AppointmentListItem(
        id: json['id'] as int,
        number: json['appointmentNumber'] as String,
        startUtc: parseUtc(json['scheduledStartUtc'] as String),
        endUtc: parseUtc(json['scheduledEndUtc'] as String),
        status: AppointmentStatus.parse(json['status']),
        paymentStatus: PaymentState.parse(json['paymentStatus']),
        totalPrice: toDouble(json['totalPrice']),
        vehicleId: json['vehicleId'] as int,
        vehicleDisplay: json['vehicleDisplay'] as String,
        assignedEmployeeName: json['assignedEmployeeName'] as String?,
        serviceNames: toStrings(json['serviceNames']),
      );

  final int id;
  final String number;
  final DateTime startUtc;
  final DateTime endUtc;
  final AppointmentStatus status;
  final PaymentState paymentStatus;
  final double totalPrice;
  final int vehicleId;
  final String vehicleDisplay;
  final String? assignedEmployeeName;
  final List<String> serviceNames;

  String get title => serviceNames.isEmpty
      ? number
      : serviceNames.length == 1
          ? serviceNames.first
          : '${serviceNames.first} i još ${serviceNames.length - 1}';
}

class AppointmentLine {
  const AppointmentLine({required this.serviceId, required this.serviceName, required this.durationMinutes, required this.lineTotal});

  factory AppointmentLine.fromJson(Json json) => AppointmentLine(
        serviceId: json['serviceId'] as int,
        serviceName: json['serviceName'] as String,
        durationMinutes: json['durationMinutes'] as int,
        lineTotal: toDouble(json['lineTotal']),
      );

  final int serviceId;
  final String serviceName;
  final int durationMinutes;
  final double lineTotal;
}

class StatusChange {
  const StatusChange({required this.toStatus, required this.changedAtUtc, required this.changedByName, required this.reason});

  factory StatusChange.fromJson(Json json) => StatusChange(
        toStatus: AppointmentStatus.parse(json['toStatus']),
        changedAtUtc: parseUtc(json['changedAtUtc'] as String),
        changedByName: json['changedByName'] as String,
        reason: json['reason'] as String?,
      );

  final AppointmentStatus toStatus;
  final DateTime changedAtUtc;
  final String changedByName;
  final String? reason;
}

/// The detail of one appointment: the "detail" half of the master-detail form.
class AppointmentDetail extends AppointmentListItem {
  const AppointmentDetail({
    required super.id,
    required super.number,
    required super.startUtc,
    required super.endUtc,
    required super.status,
    required super.paymentStatus,
    required super.totalPrice,
    required super.vehicleId,
    required super.vehicleDisplay,
    required super.assignedEmployeeName,
    required super.serviceNames,
    required this.customerNotes,
    required this.cancellationReason,
    required this.assignedEmployeePhone,
    required this.items,
    required this.history,
    required this.hasReview,
    required this.allowedActions,
    required this.changeBlockedReason,
    required this.canBePaid,
    required this.paymentBlockedReason,
  });

  factory AppointmentDetail.fromJson(dynamic json) {
    final map = json as Json;
    final head = AppointmentListItem.fromJson(map);
    return AppointmentDetail(
      id: head.id,
      number: head.number,
      startUtc: head.startUtc,
      endUtc: head.endUtc,
      status: head.status,
      paymentStatus: head.paymentStatus,
      totalPrice: head.totalPrice,
      vehicleId: head.vehicleId,
      vehicleDisplay: head.vehicleDisplay,
      assignedEmployeeName: head.assignedEmployeeName,
      serviceNames: head.serviceNames,
      customerNotes: map['customerNotes'] as String?,
      cancellationReason: map['cancellationReason'] as String?,
      assignedEmployeePhone: map['assignedEmployeePhone'] as String?,
      items: toList(map['items'], AppointmentLine.fromJson),
      history: toList(map['history'], StatusChange.fromJson),
      hasReview: map['hasReview'] as bool,
      allowedActions: toStrings(map['allowedActions']),
      changeBlockedReason: map['changeBlockedReason'] as String?,
      canBePaid: map['canBePaid'] as bool,
      paymentBlockedReason: map['paymentBlockedReason'] as String?,
    );
  }

  final String? customerNotes;
  final String? cancellationReason;
  final String? assignedEmployeePhone;
  final List<AppointmentLine> items;
  final List<StatusChange> history;
  final bool hasReview;
  final List<String> allowedActions;
  final String? changeBlockedReason;
  final bool canBePaid;
  final String? paymentBlockedReason;

  int get durationMinutes => items.fold(0, (sum, item) => sum + item.durationMinutes);

  bool can(String action) => allowedActions.contains(action);
}

class AppointmentSummary {
  const AppointmentSummary({required this.total, required this.completed, required this.active});

  factory AppointmentSummary.fromJson(dynamic json) {
    final map = json as Json;
    return AppointmentSummary(
      total: map['total'] as int,
      completed: map['completed'] as int,
      active: map['active'] as int,
    );
  }

  final int total;
  final int completed;
  final int active;
}

class Slot {
  const Slot({required this.startUtc, required this.isAvailable, required this.freePlaces, required this.unavailableReason});

  factory Slot.fromJson(Json json) => Slot(
        startUtc: parseUtc(json['startUtc'] as String),
        isAvailable: json['isAvailable'] as bool,
        freePlaces: json['freePlaces'] as int,
        unavailableReason: json['unavailableReason'] as String?,
      );

  final DateTime startUtc;
  final bool isAvailable;
  final int freePlaces;
  final String? unavailableReason;
}
