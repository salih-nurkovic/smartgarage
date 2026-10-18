import '../../core/formatting.dart';
import 'common.dart';

/// The API sends enums by name; Dart names start lower-case.
T byApiName<T extends Enum>(List<T> values, Object? name) {
  final value = name as String;
  return values.byName('${value[0].toLowerCase()}${value.substring(1)}');
}

String apiNameOf(Enum value) => '${value.name[0].toUpperCase()}${value.name.substring(1)}';

enum AppointmentStatus {
  pending('Na čekanju'),
  confirmed('Potvrđeno'),
  inProgress('U toku'),
  completed('Završeno'),
  cancelled('Otkazano');

  const AppointmentStatus(this.label);

  final String label;

  static AppointmentStatus parse(Object? name) => byApiName(values, name);

  bool get isActive => this == pending || this == confirmed || this == inProgress;
}

enum PaymentState {
  unpaid('Nije plaćeno'),
  pendingPayment('Plaćanje u obradi'),
  paid('Plaćeno'),
  partiallyRefunded('Djelimično vraćeno'),
  refunded('Vraćeno');

  const PaymentState(this.label);

  final String label;

  static PaymentState parse(Object? name) => byApiName(values, name);
}

/// The actions the server allows on an appointment, by the names it sends.
abstract final class AppointmentActions {
  static const confirm = 'Confirm';
  static const start = 'Start';
  static const complete = 'Complete';
  static const cancel = 'Cancel';
  static const reschedule = 'Reschedule';
  static const assign = 'Assign';
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
    required this.vehicleDisplay,
    required this.customerFullName,
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
    vehicleDisplay: json['vehicleDisplay'] as String,
    customerFullName: json['customerFullName'] as String,
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
  final String vehicleDisplay;
  final String customerFullName;
  final String? assignedEmployeeName;
  final List<String> serviceNames;

  String get services => serviceNames.join(', ');
}

class AppointmentLine {
  const AppointmentLine({
    required this.serviceId,
    required this.serviceName,
    required this.unitPrice,
    required this.durationMinutes,
    required this.lineTotal,
  });

  factory AppointmentLine.fromJson(Json json) => AppointmentLine(
    serviceId: json['serviceId'] as int,
    serviceName: json['serviceName'] as String,
    unitPrice: toDouble(json['unitPrice']),
    durationMinutes: json['durationMinutes'] as int,
    lineTotal: toDouble(json['lineTotal']),
  );

  final int serviceId;
  final String serviceName;
  final double unitPrice;
  final int durationMinutes;
  final double lineTotal;
}

class StatusChange {
  const StatusChange({
    required this.toStatus,
    required this.changedAtUtc,
    required this.changedByName,
    required this.reason,
  });

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

/// One appointment with everything the staff can see and do with it.
class AppointmentDetail extends AppointmentListItem {
  const AppointmentDetail({
    required super.id,
    required super.number,
    required super.startUtc,
    required super.endUtc,
    required super.status,
    required super.paymentStatus,
    required super.totalPrice,
    required super.vehicleDisplay,
    required super.customerFullName,
    required super.assignedEmployeeName,
    required super.serviceNames,
    required this.customerNotes,
    required this.internalNotes,
    required this.mileageAtAppointmentKm,
    required this.cancellationReason,
    required this.items,
    required this.history,
    required this.allowedActions,
    required this.changeBlockedReason,
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
      vehicleDisplay: head.vehicleDisplay,
      customerFullName: head.customerFullName,
      assignedEmployeeName: head.assignedEmployeeName,
      serviceNames: head.serviceNames,
      customerNotes: map['customerNotes'] as String?,
      internalNotes: map['internalNotes'] as String?,
      mileageAtAppointmentKm: map['mileageAtAppointmentKm'] as int?,
      cancellationReason: map['cancellationReason'] as String?,
      items: toList(map['items'], AppointmentLine.fromJson),
      history: toList(map['history'], StatusChange.fromJson),
      allowedActions: toStrings(map['allowedActions']),
      changeBlockedReason: map['changeBlockedReason'] as String?,
    );
  }

  final String? customerNotes;
  final String? internalNotes;
  final int? mileageAtAppointmentKm;
  final String? cancellationReason;
  final List<AppointmentLine> items;
  final List<StatusChange> history;
  final List<String> allowedActions;
  final String? changeBlockedReason;

  int get durationMinutes => items.fold(0, (sum, item) => sum + item.durationMinutes);

  bool can(String action) => allowedActions.contains(action);
}

class AppointmentSummary {
  const AppointmentSummary({
    required this.total,
    required this.pending,
    required this.confirmed,
    required this.inProgress,
    required this.completed,
  });

  factory AppointmentSummary.fromJson(dynamic json) {
    final map = json as Json;
    return AppointmentSummary(
      total: map['total'] as int,
      pending: map['pending'] as int,
      confirmed: map['confirmed'] as int,
      inProgress: map['inProgress'] as int,
      completed: map['completed'] as int,
    );
  }

  final int total;
  final int pending;
  final int confirmed;
  final int inProgress;
  final int completed;
}

class Slot {
  const Slot({
    required this.startUtc,
    required this.isAvailable,
    required this.freePlaces,
    required this.unavailableReason,
  });

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
