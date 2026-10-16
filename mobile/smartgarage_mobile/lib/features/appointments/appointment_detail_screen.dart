import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/status.dart';
import '../../data/models/appointment.dart';
import '../../data/models/lookup.dart';
import '../payments/payment_flow.dart';

/// The detail half of the master-detail form: everything about one appointment, and what the
/// customer can do with it. The server decides which actions are allowed and says why not.
class AppointmentDetailScreen extends ConsumerStatefulWidget {
  const AppointmentDetailScreen({required this.appointmentId, super.key});

  final int appointmentId;

  @override
  ConsumerState<AppointmentDetailScreen> createState() => _AppointmentDetailScreenState();
}

class _AppointmentDetailScreenState extends ConsumerState<AppointmentDetailScreen> {
  var _working = false;

  void _reload() => ref.read(appointmentsVersionProvider.notifier).bump();

  Future<void> _run(Future<String> Function() action) async {
    setState(() => _working = true);
    try {
      final message = await action();
      if (mounted) {
        showMessage(context, message);
      }
    } on Object catch (error) {
      if (mounted) {
        showMessage(context, errorMessage(error), error: true);
      }
    } finally {
      _reload();
      if (mounted) {
        setState(() => _working = false);
      }
    }
  }

  Future<void> _cancel(AppointmentDetail appointment) async {
    final reason = await askReason(
      context,
      title: 'Otkazati rezervaciju?',
      message: 'Rezervacija ${appointment.number} bit će otkazana. Ovo se ne može poništiti.',
      fieldLabel: 'Razlog otkazivanja',
      confirmLabel: 'Otkaži rezervaciju',
      validator: (v) => Validators.reason(v, 'Razlog otkazivanja'),
      destructive: true,
    );
    if (reason == null) {
      return;
    }
    await _run(() async {
      await ref.read(appointmentRepositoryProvider).cancel(appointment.id, reason);
      return 'Rezervacija je otkazana.';
    });
  }

  Future<void> _pay(AppointmentDetail appointment, MoneyFormat money) async {
    final confirmed = await confirm(
      context,
      title: 'Platiti servis?',
      message: 'Iznos ${money(appointment.totalPrice)} za rezervaciju ${appointment.number} '
          'bit će naplaćen sa vaše kartice.',
      confirmLabel: 'Plati',
    );
    if (!confirmed) {
      return;
    }
    await _run(() async {
      final (_, message) = await PaymentFlow(ref.read(paymentRepositoryProvider)).pay(appointment.id);
      return message;
    });
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(appointmentDetailProvider(widget.appointmentId));
    final garage = ref.watch(lookupProvider).value?.garage;
    final money = ref.watch(moneyProvider);

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Detalji rezervacije'),
            if (detail.value case final appointment?)
              Text(
                'Rezervacija ${appointment.number}',
                style: const TextStyle(fontSize: 13, color: AppColors.textSecondary, fontWeight: FontWeight.w400),
              ),
          ],
        ),
      ),
      body: AsyncBody<AppointmentDetail>(
        value: detail,
        onRetry: _reload,
        builder: (appointment) => RefreshIndicator(
          onRefresh: () async => _reload(),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 32),
            children: [
              _StatusCard(appointment: appointment),
              const SizedBox(height: 16),
              _ServicesSection(appointment: appointment, money: money),
              const SizedBox(height: 16),
              _DateSection(appointment: appointment),
              const SizedBox(height: 16),
              _ContactSection(appointment: appointment, garage: garage),
              const SizedBox(height: 16),
              _PaymentSection(
                appointment: appointment,
                money: money,
                busy: _working,
                onPay: () => _pay(appointment, money),
              ),
              if (appointment.can(AppointmentActions.leaveReview) || appointment.hasReview) ...[
                const SizedBox(height: 16),
                _ReviewSection(appointment: appointment),
              ],
              if (appointment.history.isNotEmpty) ...[
                const SizedBox(height: 16),
                _HistorySection(appointment: appointment),
              ],
              if (appointment.status.isActive) ...[
                const SizedBox(height: 20),
                _ChangeButtons(
                  appointment: appointment,
                  busy: _working,
                  onCancel: () => _cancel(appointment),
                  onReschedule: () => context.push('/appointments/${appointment.id}/reschedule'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.appointment});

  final AppointmentDetail appointment;

  @override
  Widget build(BuildContext context) {
    final status = appointment.status;
    final (title, subtitle) = status.headline;

    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              IconTile(icon: status.icon, color: status.color, size: 48),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                    Text(subtitle, style: const TextStyle(color: AppColors.textSecondary)),
                  ],
                ),
              ),
            ],
          ),
          if (appointment.cancellationReason case final reason?) ...[
            const SizedBox(height: 12),
            Text('Razlog: $reason', style: const TextStyle(color: AppColors.danger)),
          ],
        ],
      ),
    );
  }
}

class _ServicesSection extends StatelessWidget {
  const _ServicesSection({required this.appointment, required this.money});

  final AppointmentDetail appointment;
  final MoneyFormat money;

  @override
  Widget build(BuildContext context) => SectionCard(
        title: 'Detalji usluge',
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final item in appointment.items)
              InfoLine(
                icon: Icons.build_outlined,
                label: 'Usluga',
                value: item.serviceName,
                detail: '${money(item.lineTotal)} • ${Fmt.duration(item.durationMinutes)}',
              ),
            InfoLine(icon: Icons.directions_car_outlined, label: 'Vozilo', value: appointment.vehicleDisplay),
            if (appointment.customerNotes case final notes? when notes.isNotEmpty)
              InfoLine(icon: Icons.notes, label: 'Vaša napomena', value: notes),
          ],
        ),
      );
}

class _DateSection extends StatelessWidget {
  const _DateSection({required this.appointment});

  final AppointmentDetail appointment;

  @override
  Widget build(BuildContext context) {
    final sameDay = appointment.startUtc.day == appointment.endUtc.day &&
        appointment.startUtc.month == appointment.endUtc.month;

    return SectionCard(
      title: 'Datum i vrijeme',
      child: Column(
        children: [
          Row(
            children: [
              Expanded(child: _Tile(icon: Icons.calendar_today_outlined, label: 'Datum', value: Fmt.date(appointment.startUtc))),
              const SizedBox(width: 12),
              Expanded(child: _Tile(icon: Icons.schedule, label: 'Vrijeme', value: Fmt.time(appointment.startUtc))),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _Tile(icon: Icons.timelapse, label: 'Trajanje', value: Fmt.duration(appointment.durationMinutes)),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _Tile(
                  icon: Icons.flag_outlined,
                  label: 'Završetak',
                  value: sameDay ? Fmt.time(appointment.endUtc) : Fmt.dateTime(appointment.endUtc),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(color: AppColors.surfaceRaised, borderRadius: BorderRadius.circular(10)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 16, color: AppColors.accent),
                const SizedBox(width: 6),
                Text(label, style: const TextStyle(color: AppColors.textSecondary, fontSize: 13)),
              ],
            ),
            const SizedBox(height: 8),
            Text(value, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
          ],
        ),
      );
}

class _ContactSection extends StatelessWidget {
  const _ContactSection({required this.appointment, required this.garage});

  final AppointmentDetail appointment;
  final GarageInfo? garage;

  @override
  Widget build(BuildContext context) => SectionCard(
        title: 'Lokacija i kontakt',
        child: Column(
          children: [
            if (garage case final g?) InfoLine(icon: Icons.place_outlined, label: 'Lokacija', value: g.name, detail: g.address),
            InfoLine(
              icon: Icons.person_outline,
              label: 'Mehaničar',
              value: appointment.assignedEmployeeName ?? 'Bit će dodijeljen nakon potvrde',
            ),
            InfoLine(
              icon: Icons.phone_outlined,
              label: 'Telefon',
              value: appointment.assignedEmployeePhone ?? garage?.phone ?? '–',
            ),
          ],
        ),
      );
}

class _PaymentSection extends StatelessWidget {
  const _PaymentSection({required this.appointment, required this.money, required this.busy, required this.onPay});

  final AppointmentDetail appointment;
  final MoneyFormat money;
  final bool busy;
  final VoidCallback onPay;

  @override
  Widget build(BuildContext context) {
    final payment = appointment.paymentStatus;

    return SectionCard(
      title: 'Plaćanje',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          KeyValueRow(label: 'Cijena usluge', value: money(appointment.totalPrice)),
          const Divider(height: 20),
          Row(
            children: [
              const Expanded(child: Text('Status plaćanja', style: TextStyle(fontWeight: FontWeight.w700))),
              Icon(payment.isPaid ? Icons.check_circle_outline : Icons.payments_outlined, color: payment.color, size: 20),
              const SizedBox(width: 6),
              Text(payment.label, style: TextStyle(color: payment.color, fontWeight: FontWeight.w800)),
            ],
          ),
          if (payment.isPaid) const KeyValueRow(label: 'Način plaćanja', value: 'Kartica (Stripe)'),
          // The button exists only while something is owed; a paid appointment shows its state.
          if (!payment.isPaid) ...[
            const SizedBox(height: 14),
            BusyButton(
              label: 'Plati ${money(appointment.totalPrice)}',
              icon: Icons.lock_outline,
              busy: busy,
              onPressed: appointment.canBePaid ? onPay : null,
            ),
            if (appointment.paymentBlockedReason case final reason? when !appointment.canBePaid) BlockedReason(reason),
          ],
        ],
      ),
    );
  }
}

class _ReviewSection extends StatelessWidget {
  const _ReviewSection({required this.appointment});

  final AppointmentDetail appointment;

  @override
  Widget build(BuildContext context) => SectionCard(
        title: 'Recenzija',
        child: appointment.hasReview
            ? const Row(
                children: [
                  Icon(Icons.check_circle_outline, color: AppColors.success),
                  SizedBox(width: 8),
                  Expanded(child: Text('Hvala, recenzija za ovaj servis je ostavljena.')),
                ],
              )
            : OutlinedButton.icon(
                onPressed: () => context.push('/reviews/new?appointmentId=${appointment.id}'),
                icon: const Icon(Icons.star_outline_rounded),
                label: const Text('Ostavi recenziju'),
              ),
      );
}

class _HistorySection extends StatelessWidget {
  const _HistorySection({required this.appointment});

  final AppointmentDetail appointment;

  @override
  Widget build(BuildContext context) => SectionCard(
        title: 'Historija statusa',
        child: Column(
          children: [
            for (final change in appointment.history)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(change.toStatus.icon, size: 20, color: change.toStatus.color),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(change.toStatus.label, style: const TextStyle(fontWeight: FontWeight.w700)),
                          Text(
                            '${Fmt.dateTime(change.changedAtUtc)} • ${change.changedByName}',
                            style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                          ),
                          if (change.reason case final reason? when reason.isNotEmpty)
                            Text(reason, style: const TextStyle(fontSize: 13)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
          ],
        ),
      );
}

class _ChangeButtons extends StatelessWidget {
  const _ChangeButtons({required this.appointment, required this.busy, required this.onCancel, required this.onReschedule});

  final AppointmentDetail appointment;
  final bool busy;
  final VoidCallback onCancel;
  final VoidCallback onReschedule;

  @override
  Widget build(BuildContext context) {
    final canCancel = appointment.can(AppointmentActions.cancel);
    final canReschedule = appointment.can(AppointmentActions.reschedule);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger),
                onPressed: canCancel && !busy ? onCancel : null,
                child: const Text('Otkaži rezervaciju'),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: FilledButton(
                onPressed: canReschedule && !busy ? onReschedule : null,
                child: const Text('Promijeni termin'),
              ),
            ),
          ],
        ),
        if (appointment.changeBlockedReason case final reason? when !canCancel || !canReschedule) BlockedReason(reason),
      ],
    );
  }
}
