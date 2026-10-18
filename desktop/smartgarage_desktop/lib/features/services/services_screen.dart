import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/common.dart';
import '../../data/models/service.dart';
import 'service_form_dialog.dart';

class ServicesScreen extends ConsumerStatefulWidget {
  const ServicesScreen({super.key});

  @override
  ConsumerState<ServicesScreen> createState() => _ServicesScreenState();
}

class _ServicesScreenState extends ConsumerState<ServicesScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  int? _categoryId;
  bool? _active;
  var _version = 0;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _edit([ServiceItem? service]) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => ServiceFormDialog(service: service),
    );
    if (saved ?? false) {
      setState(() => _version++);
    }
  }

  Future<void> _delete(ServiceItem service) async {
    final ok = await confirm(
      context,
      title: 'Obrisati uslugu?',
      message: '„${service.name}" bit će uklonjena iz kataloga.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!ok || !mounted) {
      return;
    }
    try {
      await ref.read(serviceRepositoryProvider).delete(service.id);
      if (mounted) {
        showMessage(context, 'Usluga „${service.name}" je obrisana.');
        setState(() => _version++);
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(serviceRepositoryProvider);
    final lookup = ref.watch(lookupProvider).value;
    final money = ref.watch(moneyProvider);

    // A service needs a category; without one the form could not be completed.
    final canAdd = lookup != null && lookup.categories.isNotEmpty;

    return ScreenScaffold(
      title: 'Upravljanje servisnim uslugama',
      subtitle: 'Pregledajte i upravljajte uslugama',
      actions: [
        GuardedButton(
          label: 'Dodaj uslugu',
          icon: Icons.add,
          filled: true,
          onPressed: canAdd ? _edit : null,
          disabledReason: 'Prvo dodajte barem jednu kategoriju usluga u šifarnicima.',
        ),
      ],
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      decoration: const InputDecoration(hintText: 'Pretraži usluge...', prefixIcon: Icon(Icons.search)),
                      onChanged: (value) {
                        _debounce?.cancel();
                        _debounce = Timer(const Duration(milliseconds: 400), () => setState(() => _term = value));
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 220,
                    child: DropdownButtonFormField<int?>(
                      initialValue: _categoryId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Kategorija'),
                      items: [
                        const DropdownMenuItem(child: Text('Sve kategorije')),
                        for (final c in lookup?.categories ?? const <NamedItem>[])
                          DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      onChanged: (v) => setState(() => _categoryId = v),
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 170,
                    child: DropdownButtonFormField<bool?>(
                      initialValue: _active,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: const [
                        DropdownMenuItem(child: Text('Sve')),
                        DropdownMenuItem(value: true, child: Text('Aktivne')),
                        DropdownMenuItem(value: false, child: Text('Neaktivne')),
                      ],
                      onChanged: (v) => setState(() => _active = v),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<ServiceItem>(
              reloadKey: (_term, _categoryId, _active, _version),
              fetch: (page) => repository.list(search: _term, categoryId: _categoryId, isActive: _active, page: page),
              emptyMessage: 'Nijedna usluga ne odgovara pretrazi.',
              builder: (context, items, _) => LayoutBuilder(
                builder: (context, constraints) {
                  final columns = constraints.maxWidth > 1100 ? 2 : 1;
                  final width = (constraints.maxWidth - 16 * (columns - 1)) / columns;
                  return Wrap(
                    spacing: 16,
                    runSpacing: 16,
                    children: [
                      for (final service in items)
                        SizedBox(
                          width: width,
                          child: _ServiceCard(
                            service: service,
                            money: money,
                            onEdit: () => _edit(service),
                            onDelete: () => _delete(service),
                          ),
                        ),
                    ],
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ServiceCard extends StatelessWidget {
  const _ServiceCard({required this.service, required this.money, required this.onEdit, required this.onDelete});

  final ServiceItem service;
  final MoneyFormat money;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final used = service.bookingCount > 0;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                AuthImage(url: service.imageUrl, fallbackIcon: Icons.build_outlined, width: 64, height: 64),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(service.name, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
                      const SizedBox(height: 4),
                      Text(
                        service.description,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(color: AppColors.textSecondary),
                      ),
                      const SizedBox(height: 8),
                      Pill(label: service.categoryName, color: AppColors.textSecondary),
                    ],
                  ),
                ),
                Pill(
                  label: service.isActive ? 'Aktivna' : 'Neaktivna',
                  color: service.isActive ? AppColors.success : AppColors.textSecondary,
                ),
              ],
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                _Figure(icon: Icons.schedule, label: 'Trajanje', value: Fmt.duration(service.durationMinutes)),
                _Figure(
                  icon: Icons.payments_outlined,
                  label: 'Cijena',
                  value: money(service.price),
                  color: AppColors.success,
                ),
                _Figure(
                  icon: Icons.star_outline_rounded,
                  label: 'Ocjena',
                  value: service.averageRating == null ? '—' : service.averageRating!.toStringAsFixed(1),
                  color: AppColors.warning,
                ),
                _Figure(icon: Icons.event_note_outlined, label: 'Rezervacije', value: '${service.bookingCount}'),
              ],
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onEdit,
                    icon: const Icon(Icons.edit_outlined, size: 18),
                    label: const Text('Uredi'),
                  ),
                ),
                const SizedBox(width: 12),
                GuardedButton(
                  label: 'Obriši',
                  icon: Icons.delete_outline,
                  destructive: true,
                  onPressed: used ? null : onDelete,
                  disabledReason:
                      'Usluga je na ${service.bookingCount} rezervacija i ne može se obrisati. '
                      'Označite je kao neaktivnu u uređivanju.',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({required this.icon, required this.label, required this.value, this.color = AppColors.accent});

  final IconData icon;
  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
    child: Container(
      margin: const EdgeInsets.only(right: 8),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(color: AppColors.surfaceRaised, borderRadius: BorderRadius.circular(8)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 14, color: color),
              const SizedBox(width: 4),
              Text(label, style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
            ],
          ),
          const SizedBox(height: 4),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
        ],
      ),
    ),
  );
}
