import 'package:flutter/material.dart';

import '../../data/models/appointment.dart';
import '../theme.dart';

extension AppointmentStatusStyle on AppointmentStatus {
  Color get color => switch (this) {
        AppointmentStatus.pending => AppColors.warning,
        AppointmentStatus.confirmed => AppColors.accent,
        AppointmentStatus.inProgress => AppColors.urgent,
        AppointmentStatus.completed => AppColors.success,
        AppointmentStatus.cancelled => AppColors.danger,
      };

  IconData get icon => switch (this) {
        AppointmentStatus.pending => Icons.hourglass_top_rounded,
        AppointmentStatus.confirmed => Icons.event_available_outlined,
        AppointmentStatus.inProgress => Icons.build_circle_outlined,
        AppointmentStatus.completed => Icons.check_circle_outline,
        AppointmentStatus.cancelled => Icons.cancel_outlined,
      };

  /// The headline and line under it on the detail screen's status card.
  (String, String) get headline => switch (this) {
        AppointmentStatus.pending => ('Rezervacija na čekanju', 'Servis će uskoro potvrditi vaš termin'),
        AppointmentStatus.confirmed => ('Rezervacija potvrđena', 'Vaš termin je rezervisan'),
        AppointmentStatus.inProgress => ('Servis u toku', 'Vaše vozilo je trenutno na servisu'),
        AppointmentStatus.completed => ('Servis završen', 'Hvala što ste izabrali naš servis'),
        AppointmentStatus.cancelled => ('Rezervacija otkazana', 'Ovaj termin više ne važi'),
      };
}

extension PaymentStateStyle on PaymentState {
  Color get color => switch (this) {
        PaymentState.paid => AppColors.success,
        PaymentState.pendingPayment => AppColors.warning,
        PaymentState.partiallyRefunded || PaymentState.refunded => AppColors.accent,
        PaymentState.unpaid => AppColors.textSecondary,
      };
}
