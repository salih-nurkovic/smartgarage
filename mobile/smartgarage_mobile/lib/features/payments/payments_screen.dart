import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/paged_list.dart';
import '../../data/models/payment.dart';

class PaymentsScreen extends ConsumerWidget {
  const PaymentsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final repository = ref.watch(paymentRepositoryProvider);
    final money = ref.watch(moneyProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Moja plaćanja')),
      body: PagedList<Payment>(
        reloadKey: ref.watch(appointmentsVersionProvider),
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        fetch: (page) => repository.mine(page: page),
        empty: const EmptyView(
          icon: Icons.receipt_long_outlined,
          message: 'Još nemate plaćanja. Servis se plaća nakon što je završen.',
        ),
        itemBuilder: (context, payment, _) => _PaymentCard(payment: payment, money: money),
      ),
    );
  }
}

class _PaymentCard extends StatelessWidget {
  const _PaymentCard({required this.payment, required this.money});

  final Payment payment;
  final MoneyFormat money;

  Color get _color => switch (payment.status) {
        PaymentStatus.succeeded => AppColors.success,
        PaymentStatus.failed || PaymentStatus.cancelled => AppColors.danger,
        _ => AppColors.warning,
      };

  @override
  Widget build(BuildContext context) => Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.push('/appointments/${payment.appointmentId}'),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const IconTile(icon: Icons.credit_card, size: 40),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(payment.number, style: const TextStyle(fontWeight: FontWeight.w800)),
                          Text(
                            'Rezervacija ${payment.appointmentNumber}',
                            style: const TextStyle(color: AppColors.textSecondary, fontSize: 13),
                          ),
                        ],
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text(money(payment.amount), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                        Pill(label: payment.status.label, color: _color),
                      ],
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Text(payment.serviceNames.join(', '), style: const TextStyle(color: AppColors.textSecondary)),
                const SizedBox(height: 4),
                Text(
                  payment.paidAtUtc == null
                      ? 'Kreirano: ${Fmt.dateTime(payment.createdAtUtc)}'
                      : 'Plaćeno: ${Fmt.dateTime(payment.paidAtUtc!)}',
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 13),
                ),
                if (payment.failureReason case final reason?)
                  Text(reason, style: const TextStyle(color: AppColors.danger, fontSize: 13)),
                for (final refund in payment.refunds)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Row(
                      children: [
                        const Icon(Icons.undo, size: 16, color: AppColors.accent),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            'Povrat ${money(refund.amount)} (${refund.statusLabel}), ${Fmt.date(refund.requestedAtUtc)}: ${refund.reason}',
                            style: const TextStyle(fontSize: 13),
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ),
      );
}
