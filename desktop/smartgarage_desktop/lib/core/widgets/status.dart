import 'package:flutter/material.dart';

import '../../data/models/admin.dart';
import '../../data/models/appointment.dart';
import '../theme.dart';
import 'common.dart';

extension AppointmentStatusStyle on AppointmentStatus {
  Color get color => switch (this) {
    AppointmentStatus.pending => AppColors.warning,
    AppointmentStatus.confirmed => AppColors.accent,
    AppointmentStatus.inProgress => AppColors.urgent,
    AppointmentStatus.completed => AppColors.success,
    AppointmentStatus.cancelled => AppColors.danger,
  };

  IconData get icon => switch (this) {
    AppointmentStatus.pending => Icons.error_outline,
    AppointmentStatus.confirmed => Icons.schedule,
    AppointmentStatus.inProgress => Icons.build_circle_outlined,
    AppointmentStatus.completed => Icons.check_circle_outline,
    AppointmentStatus.cancelled => Icons.cancel_outlined,
  };
}

extension PaymentStatusStyle on PaymentStatus {
  Color get color => switch (this) {
    PaymentStatus.succeeded => AppColors.success,
    PaymentStatus.failed || PaymentStatus.cancelled => AppColors.danger,
    _ => AppColors.warning,
  };

  IconData get icon => switch (this) {
    PaymentStatus.succeeded => Icons.check_circle_outline,
    PaymentStatus.failed || PaymentStatus.cancelled => Icons.cancel_outlined,
    _ => Icons.credit_card,
  };
}

/// Icon and pill together, as the status column of the mockup tables shows them.
class StatusTag extends StatelessWidget {
  const StatusTag({required this.label, required this.color, required this.icon, super.key});

  final String label;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(icon, size: 18, color: color),
      const SizedBox(width: 8),
      Flexible(
        child: Pill(label: label, color: color),
      ),
    ],
  );
}
