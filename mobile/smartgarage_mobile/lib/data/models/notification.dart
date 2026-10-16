import '../../core/formatting.dart';
import 'common.dart';

enum NotificationKind { appointment, reminder, payment, account }

class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.body,
    required this.isRead,
    required this.createdAtUtc,
    required this.relatedEntityType,
    required this.relatedEntityId,
  });

  factory AppNotification.fromJson(Json json) => AppNotification(
        id: json['id'] as int,
        type: json['type'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        isRead: json['isRead'] as bool,
        createdAtUtc: parseUtc(json['createdAtUtc'] as String),
        relatedEntityType: json['relatedEntityType'] as String?,
        relatedEntityId: json['relatedEntityId'] as int?,
      );

  final int id;

  /// The server's `NotificationType` name, e.g. `AppointmentConfirmed`.
  final String type;
  final String title;
  final String body;
  final bool isRead;
  final DateTime createdAtUtc;
  final String? relatedEntityType;
  final int? relatedEntityId;

  NotificationKind get kind => switch (type) {
        'AppointmentReminder' || 'ServiceReminder' || 'ReviewRequested' => NotificationKind.reminder,
        'PaymentSucceeded' || 'PaymentFailed' || 'RefundIssued' => NotificationKind.payment,
        'AccountStatusChanged' => NotificationKind.account,
        _ => NotificationKind.appointment,
      };

  /// The appointment this notification is about, when there is one to open.
  int? get appointmentId => relatedEntityType == 'Appointment' ? relatedEntityId : null;
}
