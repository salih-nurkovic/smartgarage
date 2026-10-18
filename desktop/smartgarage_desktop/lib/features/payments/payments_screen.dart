import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../core/widgets/status.dart';
import '../../data/models/admin.dart';

final _summaryProvider = FutureProvider.autoDispose<PaymentSummary>((ref) {
  ref.watch(appointmentsVersionProvider);
  return ref.watch(paymentRepositoryProvider).summary();
});

class PaymentsScreen extends ConsumerStatefulWidget {
  const PaymentsScreen({super.key});

  @override
  ConsumerState<PaymentsScreen> createState() => _PaymentsScreenState();
}

class _PaymentsScreenState extends ConsumerState<PaymentsScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  PaymentStatus? _status;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _open(Payment payment) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => _PaymentDialog(paymentId: payment.id),
    );
    if (changed ?? false) {
      ref.read(appointmentsVersionProvider.notifier).bump();
    }
  }

  @override
  Widget build(BuildContext context) {
    final summary = ref.watch(_summaryProvider).value;
    final version = ref.watch(appointmentsVersionProvider);
    final repository = ref.watch(paymentRepositoryProvider);
    final money = ref.watch(moneyProvider);

    return ScreenScaffold(
      title: 'Upravljanje plaćanjima',
      subtitle: 'Pregledajte sve transakcije i prihode',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Tooltip(
                  message: 'Naplaćeno kroz Stripe, umanjeno za izvršene povrate',
                  child: StatCard(label: 'Ukupan prihod', value: summary == null ? '–' : money(summary.netRevenue)),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(
                  label: 'Uspješne transakcije',
                  value: '${summary?.succeeded ?? '–'}',
                  valueColor: AppColors.success,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(
                  label: 'U obradi',
                  value: '${summary?.inProgress ?? '–'}',
                  valueColor: AppColors.warning,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: StatCard(label: 'Neuspješne', value: '${summary?.failed ?? '–'}', valueColor: AppColors.danger),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      decoration: const InputDecoration(
                        hintText: 'Pretraži transakcije po broju, rezervaciji ili kupcu...',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: (value) {
                        _debounce?.cancel();
                        _debounce = Timer(const Duration(milliseconds: 400), () => setState(() => _term = value));
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 200,
                    child: DropdownButtonFormField<PaymentStatus?>(
                      initialValue: _status,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: [
                        const DropdownMenuItem(child: Text('Svi statusi')),
                        for (final s in PaymentStatus.values) DropdownMenuItem(value: s, child: Text(s.label)),
                      ],
                      onChanged: (v) => setState(() => _status = v),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<Payment>(
              reloadKey: (_term, _status, version),
              fetch: (page) => repository.list(search: _term, status: _status, page: page),
              emptyMessage: 'Nema transakcija koje odgovaraju filterima.',
              builder: (context, items, _) => SimpleTable<Payment>(
                items: items,
                onTap: _open,
                columns: [
                  TableColumn(
                    'ID transakcije',
                    (p) => Text(p.number, style: const TextStyle(color: AppColors.accent)),
                    flex: 2,
                  ),
                  TableColumn(
                    'Korisnik',
                    (p) => Text(p.payerFullName, style: const TextStyle(fontWeight: FontWeight.w700)),
                  ),
                  TableColumn(
                    'Usluga',
                    (p) => Text(p.serviceNames.join(', '), overflow: TextOverflow.ellipsis, maxLines: 2),
                    flex: 2,
                  ),
                  TableColumn(
                    'Iznos',
                    (p) => Text(money(p.amount), style: const TextStyle(fontWeight: FontWeight.w700)),
                  ),
                  TableColumn('Datum', (p) => Text(Fmt.date(p.paidAtUtc ?? p.createdAtUtc))),
                  TableColumn('Način plaćanja', (_) => const Text('Kartica (Stripe)')),
                  TableColumn(
                    'Status',
                    (p) => StatusTag(
                      label: p.refundedAmount > 0
                          ? '${p.status.label}, vraćeno ${money(p.refundedAmount)}'
                          : p.status.label,
                      color: p.status.color,
                      icon: p.status.icon,
                    ),
                    flex: 2,
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

final _paymentProvider = FutureProvider.autoDispose.family<Payment, int>(
  (ref, id) => ref.watch(paymentRepositoryProvider).get(id),
);

/// One payment with its refunds, and the refund form.
class _PaymentDialog extends ConsumerStatefulWidget {
  const _PaymentDialog({required this.paymentId});

  final int paymentId;

  @override
  ConsumerState<_PaymentDialog> createState() => _PaymentDialogState();
}

class _PaymentDialogState extends ConsumerState<_PaymentDialog> {
  final _form = GlobalKey<FormState>();
  final _amount = TextEditingController();
  final _reason = TextEditingController();
  var _busy = false;
  var _changed = false;
  String? _error;
  Map<String, String> _fieldErrors = const {};

  @override
  void dispose() {
    _amount.dispose();
    _reason.dispose();
    super.dispose();
  }

  Future<void> _refund(Payment payment, MoneyFormat money) async {
    if (!_form.currentState!.validate()) {
      return;
    }
    final amount = _amount.text.trim().isEmpty ? null : double.parse(_amount.text.trim().replaceAll(',', '.'));
    final ok = await confirm(
      context,
      title: 'Izvršiti povrat?',
      message:
          'Kupcu ${payment.payerFullName} bit će vraćeno ${money(amount ?? payment.refundableAmount)} '
          'na karticu kroz Stripe. Ovo se ne može poništiti.',
      confirmLabel: 'Vrati novac',
      destructive: true,
    );
    if (!ok) {
      return;
    }

    setState(() {
      _busy = true;
      _error = null;
      _fieldErrors = const {};
    });
    try {
      await ref.read(paymentRepositoryProvider).refund(payment.id, amount: amount, reason: _reason.text.trim());
      _changed = true;
      _amount.clear();
      _reason.clear();
      ref.invalidate(_paymentProvider(widget.paymentId));
      if (mounted) {
        showMessage(context, 'Povrat za ${payment.number} je izvršen.');
      }
    } on ApiException catch (error) {
      setState(() {
        _fieldErrors = error.fieldErrors;
        _error = error.fieldErrors.isEmpty ? error.message : null;
      });
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final payment = ref.watch(_paymentProvider(widget.paymentId));
    final money = ref.watch(moneyProvider);

    return AlertDialog(
      titlePadding: const EdgeInsets.fromLTRB(24, 16, 12, 0),
      title: Row(
        children: [
          const Expanded(child: Text('Detalji plaćanja')),
          IconButton(
            tooltip: 'Zatvori',
            onPressed: () => Navigator.of(context).pop(_changed),
            icon: const Icon(Icons.close),
          ),
        ],
      ),
      content: SizedBox(
        width: 560,
        child: AsyncBody<Payment>(
          value: payment,
          onRetry: () => ref.invalidate(_paymentProvider(widget.paymentId)),
          builder: (p) => SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                KeyValueRow(label: 'Transakcija', value: p.number),
                KeyValueRow(label: 'Kupac', value: p.payerFullName),
                KeyValueRow(label: 'Usluge', value: p.serviceNames.join('\n')),
                KeyValueRow(
                  label: 'Status',
                  value: p.status.label,
                  valueStyle: TextStyle(color: p.status.color, fontWeight: FontWeight.w700),
                ),
                KeyValueRow(label: 'Naplaćeno', value: money(p.amountCharged)),
                KeyValueRow(label: 'Vraćeno', value: money(p.refundedAmount)),
                KeyValueRow(label: 'Može se vratiti', value: money(p.refundableAmount)),
                if (p.failureReason case final reason?) KeyValueRow(label: 'Razlog neuspjeha', value: reason),
                Align(
                  alignment: Alignment.centerLeft,
                  child: TextButton.icon(
                    onPressed: () {
                      Navigator.of(context).pop(_changed);
                      context.push('/appointments/${p.appointmentId}');
                    },
                    icon: const Icon(Icons.open_in_new, size: 16),
                    label: Text('Rezervacija ${p.appointmentNumber}'),
                  ),
                ),
                if (p.refunds.isNotEmpty) ...[
                  const Divider(height: 24),
                  const Text('Povrati', style: TextStyle(fontWeight: FontWeight.w700)),
                  for (final refund in p.refunds)
                    ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.undo, color: AppColors.accent),
                      title: Text('${money(refund.amount)} — ${refund.statusLabel}'),
                      subtitle: Text('${Fmt.dateTime(refund.requestedAtUtc)} • ${refund.reason}'),
                    ),
                ],
                const Divider(height: 24),
                if (p.refundableAmount <= 0)
                  BlockedReason(
                    p.status == PaymentStatus.succeeded
                        ? 'Cijeli naplaćeni iznos je već vraćen.'
                        : 'Povrat je moguć samo za uspješno plaćanje.',
                  )
                else
                  Form(
                    key: _form,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text('Povrat novca', style: TextStyle(fontWeight: FontWeight.w700)),
                        const SizedBox(height: 8),
                        TextFormField(
                          controller: _amount,
                          decoration: InputDecoration(
                            labelText: 'Iznos (prazno = cijeli preostali iznos)',
                            helperText: 'Najviše ${money(p.refundableAmount)}',
                          ),
                          validator: (v) {
                            if ((v ?? '').trim().isEmpty) {
                              return null;
                            }
                            final amount = double.tryParse(v!.trim().replaceAll(',', '.'));
                            return amount == null || amount <= 0 || amount > p.refundableAmount
                                ? 'Unesite iznos veći od 0 i najviše ${money(p.refundableAmount)}.'
                                : null;
                          },
                          forceErrorText: _fieldErrors['amount'],
                        ),
                        const SizedBox(height: 12),
                        TextFormField(
                          controller: _reason,
                          decoration: const InputDecoration(labelText: 'Razlog povrata'),
                          validator: (v) => Validators.reason(v, 'Razlog povrata'),
                          forceErrorText: _fieldErrors['reason'],
                        ),
                        const SizedBox(height: 12),
                        FormErrorText(_error),
                        BusyButton(label: 'Vrati novac', busy: _busy, onPressed: () => _refund(p, money)),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
