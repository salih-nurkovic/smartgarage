import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/status.dart';
import '../../data/models/appointment.dart';
import 'appointment_dialogs.dart';

final appointmentDetailProvider = FutureProvider.autoDispose.family<AppointmentDetail, int>((ref, id) {
  ref.watch(appointmentsVersionProvider);
  return ref.watch(appointmentRepositoryProvider).detail(id);
});

/// One appointment and what the staff can do with it. Which actions are allowed is decided by
/// the server's state machine; the buttons follow it and say why when they cannot be used.
class AppointmentDetailScreen extends ConsumerStatefulWidget {
  const AppointmentDetailScreen({required this.appointmentId, super.key});

  final int appointmentId;

  @override
  ConsumerState<AppointmentDetailScreen> createState() => _AppointmentDetailScreenState();
}

class _AppointmentDetailScreenState extends ConsumerState<AppointmentDetailScreen> {
  var _working = false;

  Future<void> _run(Future<void> Function() action, String success) async {
    setState(() => _working = true);
    try {
      await action();
      ref.read(appointmentsVersionProvider.notifier).bump();
      if (mounted) {
        showMessage(context, success);
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    } finally {
      if (mounted) {
        setState(() => _working = false);
      }
    }
  }

  Future<void> _confirm(AppointmentDetail a) async {
    final ok = await confirm(
      context,
      title: 'Potvrditi rezervaciju?',
      message: '${a.number}: ${a.customerFullName}, ${Fmt.dateTime(a.startUtc)}. Kupac dobija obavijest.',
      confirmLabel: 'Potvrdi',
    );
    if (ok) {
      await _run(() => ref.read(appointmentRepositoryProvider).confirm(a.id), 'Rezervacija ${a.number} je potvrđena.');
    }
  }

  Future<void> _start(AppointmentDetail a) async {
    final ok = await confirm(
      context,
      title: 'Započeti servis?',
      message: 'Rezervacija ${a.number} prelazi u status „U toku".',
      confirmLabel: 'Započni',
    );
    if (ok) {
      await _run(() => ref.read(appointmentRepositoryProvider).start(a.id), 'Servis za ${a.number} je započet.');
    }
  }

  Future<void> _cancel(AppointmentDetail a) async {
    final reason = await askReason(
      context,
      title: 'Otkazati rezervaciju?',
      message: 'Rezervacija ${a.number} bit će otkazana, a kupac obaviješten sa razlogom.',
      fieldLabel: 'Razlog otkazivanja',
      confirmLabel: 'Otkaži rezervaciju',
      validator: (v) => Validators.reason(v, 'Razlog otkazivanja'),
      destructive: true,
    );
    if (reason != null) {
      await _run(
        () => ref.read(appointmentRepositoryProvider).cancel(a.id, reason),
        'Rezervacija ${a.number} je otkazana.',
      );
    }
  }

  Future<void> _assign(AppointmentDetail a) async {
    final done = await showDialog<bool>(
      context: context,
      builder: (_) => AssignDialog(appointment: a),
    );
    if (done ?? false) {
      ref.read(appointmentsVersionProvider.notifier).bump();
    }
  }

  Future<void> _reschedule(AppointmentDetail a) async {
    final done = await showDialog<bool>(
      context: context,
      builder: (_) => RescheduleDialog(appointment: a),
    );
    if (done ?? false) {
      ref.read(appointmentsVersionProvider.notifier).bump();
    }
  }

  Future<void> _complete(AppointmentDetail a) async {
    final done = await showDialog<bool>(
      context: context,
      builder: (_) => CompleteDialog(appointment: a),
    );
    if (done ?? false) {
      ref.read(appointmentsVersionProvider.notifier).bump();
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(appointmentDetailProvider(widget.appointmentId));
    final money = ref.watch(moneyProvider);

    return ScreenScaffold(
      title: 'Detalji rezervacije',
      subtitle: detail.value == null ? null : 'Rezervacija ${detail.value!.number}',
      body: AsyncBody<AppointmentDetail>(
        value: detail,
        onRetry: () => ref.invalidate(appointmentDetailProvider(widget.appointmentId)),
        builder: (a) {
          String? blocked(String action) =>
              a.can(action) ? null : a.changeBlockedReason ?? 'Nije moguće u statusu „${a.status.label}".';

          return ListView(
            padding: const EdgeInsets.only(bottom: 28),
            children: [
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      StatusTag(label: a.status.label, color: a.status.color, icon: a.status.icon),
                      const SizedBox(width: 12),
                      GuardedButton(
                        label: 'Potvrdi',
                        icon: Icons.check,
                        filled: true,
                        onPressed: !_working && a.can(AppointmentActions.confirm) ? () => _confirm(a) : null,
                        disabledReason: blocked(AppointmentActions.confirm),
                      ),
                      GuardedButton(
                        label: 'Dodijeli mehaničara',
                        icon: Icons.engineering_outlined,
                        onPressed: !_working && a.can(AppointmentActions.assign) ? () => _assign(a) : null,
                        disabledReason:
                            blocked(AppointmentActions.assign) ?? 'Samo administrator dodjeljuje mehaničare.',
                      ),
                      GuardedButton(
                        label: 'Pomjeri termin',
                        icon: Icons.event_repeat,
                        onPressed: !_working && a.can(AppointmentActions.reschedule) ? () => _reschedule(a) : null,
                        disabledReason: blocked(AppointmentActions.reschedule),
                      ),
                      GuardedButton(
                        label: 'Započni',
                        icon: Icons.play_arrow,
                        onPressed: !_working && a.can(AppointmentActions.start) ? () => _start(a) : null,
                        disabledReason: blocked(AppointmentActions.start),
                      ),
                      GuardedButton(
                        label: 'Završi',
                        icon: Icons.flag_outlined,
                        onPressed: !_working && a.can(AppointmentActions.complete) ? () => _complete(a) : null,
                        disabledReason: blocked(AppointmentActions.complete),
                      ),
                      GuardedButton(
                        label: 'Otkaži',
                        icon: Icons.cancel_outlined,
                        destructive: true,
                        onPressed: !_working && a.can(AppointmentActions.cancel) ? () => _cancel(a) : null,
                        disabledReason: blocked(AppointmentActions.cancel),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      children: [
                        SectionCard(
                          title: 'Kupac i vozilo',
                          child: Column(
                            children: [
                              KeyValueRow(label: 'Kupac', value: a.customerFullName),
                              KeyValueRow(label: 'Vozilo', value: a.vehicleDisplay),
                              KeyValueRow(label: 'Mehaničar', value: a.assignedEmployeeName ?? 'Nije dodijeljen'),
                              if (a.mileageAtAppointmentKm case final km?)
                                KeyValueRow(label: 'Kilometraža na servisu', value: Fmt.km(km)),
                            ],
                          ),
                        ),
                        const SizedBox(height: 16),
                        SectionCard(
                          title: 'Termin',
                          child: Column(
                            children: [
                              KeyValueRow(label: 'Početak', value: Fmt.dateTime(a.startUtc)),
                              KeyValueRow(label: 'Završetak', value: Fmt.dateTime(a.endUtc)),
                              KeyValueRow(label: 'Trajanje', value: Fmt.duration(a.durationMinutes)),
                            ],
                          ),
                        ),
                        const SizedBox(height: 16),
                        SectionCard(
                          title: 'Napomene',
                          child: Column(
                            children: [
                              KeyValueRow(label: 'Napomena kupca', value: a.customerNotes ?? '—'),
                              KeyValueRow(label: 'Interna napomena', value: a.internalNotes ?? '—'),
                              if (a.cancellationReason case final reason?)
                                KeyValueRow(
                                  label: 'Razlog otkazivanja',
                                  value: reason,
                                  valueStyle: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
                                ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      children: [
                        SectionCard(
                          title: 'Usluge',
                          child: Column(
                            children: [
                              for (final item in a.items)
                                KeyValueRow(
                                  label: '${item.serviceName} (${Fmt.duration(item.durationMinutes)})',
                                  value: money(item.lineTotal),
                                ),
                              const Divider(height: 20),
                              KeyValueRow(
                                label: 'Ukupno',
                                value: money(a.totalPrice),
                                valueStyle: const TextStyle(
                                  color: AppColors.accent,
                                  fontSize: 18,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              KeyValueRow(label: 'Plaćanje', value: a.paymentStatus.label),
                            ],
                          ),
                        ),
                        const SizedBox(height: 16),
                        SectionCard(
                          title: 'Historija statusa',
                          child: Column(
                            children: [
                              for (final change in a.history)
                                ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  leading: Icon(change.toStatus.icon, color: change.toStatus.color),
                                  title: Text(
                                    change.toStatus.label,
                                    style: const TextStyle(fontWeight: FontWeight.w700),
                                  ),
                                  subtitle: Text(
                                    [
                                      '${Fmt.dateTime(change.changedAtUtc)} • ${change.changedByName}',
                                      if (change.reason case final reason? when reason.isNotEmpty) reason,
                                    ].join('\n'),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ],
          );
        },
      ),
    );
  }
}
