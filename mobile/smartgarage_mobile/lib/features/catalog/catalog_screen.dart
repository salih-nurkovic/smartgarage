import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/paged_list.dart';
import '../../data/models/common.dart';
import '../../data/models/service.dart';

class CatalogScreen extends ConsumerStatefulWidget {
  const CatalogScreen({super.key});

  @override
  ConsumerState<CatalogScreen> createState() => _CatalogScreenState();
}

class _CatalogScreenState extends ConsumerState<CatalogScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _filter = const ServiceFilter();

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  // Waits for a pause in typing, so a search is sent (and recorded) once per term rather than
  // once per keystroke.
  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 500), () => setState(() => _filter = _filter.copyWith(search: value)));
  }

  Future<void> _openFilters() async {
    final lookup = ref.read(lookupProvider).value;
    final result = await showModalBottomSheet<ServiceFilter>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.surface,
      builder: (_) => _FilterSheet(initial: _filter, categories: lookup?.categories ?? const []),
    );
    if (result != null) {
      setState(() => _filter = result.copyWith(search: _search.text));
    }
  }

  @override
  Widget build(BuildContext context) {
    final money = ref.watch(moneyProvider);
    final repository = ref.watch(catalogRepositoryProvider);
    final activeFilters = _filter.activeCount;

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            const PageHeader(title: 'Servisne usluge', subtitle: 'Izaberite uslugu koja vam je potrebna'),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Column(
                children: [
                  TextField(
                    controller: _search,
                    onChanged: _onSearchChanged,
                    textInputAction: TextInputAction.search,
                    decoration: const InputDecoration(
                      hintText: 'Pretraži usluge...',
                      prefixIcon: Icon(Icons.search),
                    ),
                  ),
                  const SizedBox(height: 10),
                  OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(46)),
                    onPressed: _openFilters,
                    icon: const Icon(Icons.filter_alt_outlined),
                    label: Text(activeFilters == 0 ? 'Filteri' : 'Filteri ($activeFilters)'),
                  ),
                ],
              ),
            ),
            Expanded(
              child: PagedList<ServiceItem>(
                reloadKey: _filter,
                fetch: (page) => repository.services(_filter, page: page),
                empty: const EmptyView(
                  icon: Icons.search_off,
                  message: 'Nijedna usluga ne odgovara pretrazi. Promijenite pojam ili filtere.',
                ),
                itemBuilder: (context, service, _) => ServiceCard(service: service, money: money),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class ServiceCard extends StatelessWidget {
  const ServiceCard({required this.service, required this.money, super.key});

  final ServiceItem service;
  final MoneyFormat money;

  @override
  Widget build(BuildContext context) => Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.push('/services/${service.id}'),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    AuthImage(url: service.imageUrl, fallbackIcon: Icons.build_outlined),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(service.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                          const SizedBox(height: 4),
                          Text(
                            service.description,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(color: AppColors.textSecondary, fontSize: 13),
                          ),
                          const SizedBox(height: 8),
                          Pill(label: service.categoryName, color: AppColors.textSecondary),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                Row(
                  children: [
                    const Icon(Icons.schedule, size: 16, color: AppColors.textSecondary),
                    const SizedBox(width: 4),
                    Expanded(child: Text(Fmt.duration(service.durationMinutes))),
                    const Icon(Icons.payments_outlined, size: 16, color: AppColors.success),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(money(service.price), style: const TextStyle(fontWeight: FontWeight.w700)),
                    ),
                    const Icon(Icons.star_rounded, size: 18, color: AppColors.warning),
                    const SizedBox(width: 4),
                    Text(
                      service.averageRating == null ? 'Bez ocjena' : service.averageRating!.toStringAsFixed(1),
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                FilledButton(
                  style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(46)),
                  onPressed: () => context.go('/book?serviceId=${service.id}'),
                  child: const Text('Zakaži termin'),
                ),
              ],
            ),
          ),
        ),
      );
}

class _FilterSheet extends StatefulWidget {
  const _FilterSheet({required this.initial, required this.categories});

  final ServiceFilter initial;
  final List<NamedItem> categories;

  @override
  State<_FilterSheet> createState() => _FilterSheetState();
}

class _FilterSheetState extends State<_FilterSheet> {
  static const _priceLimits = [50.0, 100.0, 200.0, 500.0];
  static const _durationLimits = [60, 120, 240, 480];

  late int? _categoryId = widget.initial.categoryId;
  late double? _maxPrice = widget.initial.maxPrice;
  late int? _maxDuration = widget.initial.maxDurationMinutes;
  late ServiceSort _sort = widget.initial.sort;

  @override
  Widget build(BuildContext context) => Padding(
        padding: EdgeInsets.fromLTRB(16, 20, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                const Expanded(
                  child: Text('Filteri', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
                ),
                IconButton(
                  tooltip: 'Zatvori',
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.close),
                ),
              ],
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int?>(
              initialValue: _categoryId,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Kategorija'),
              items: [
                const DropdownMenuItem(child: Text('Sve kategorije')),
                for (final c in widget.categories) DropdownMenuItem(value: c.id, child: Text(c.name)),
              ],
              onChanged: (v) => setState(() => _categoryId = v),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<double?>(
              initialValue: _maxPrice,
              decoration: const InputDecoration(labelText: 'Najviša cijena'),
              items: [
                const DropdownMenuItem(child: Text('Bez ograničenja')),
                for (final p in _priceLimits) DropdownMenuItem(value: p, child: Text('do ${p.toStringAsFixed(0)}')),
              ],
              onChanged: (v) => setState(() => _maxPrice = v),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<int?>(
              initialValue: _maxDuration,
              decoration: const InputDecoration(labelText: 'Najduže trajanje'),
              items: [
                const DropdownMenuItem(child: Text('Bez ograničenja')),
                for (final d in _durationLimits) DropdownMenuItem(value: d, child: Text('do ${Fmt.duration(d)}')),
              ],
              onChanged: (v) => setState(() => _maxDuration = v),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<ServiceSort>(
              initialValue: _sort,
              decoration: const InputDecoration(labelText: 'Sortiranje'),
              items: [for (final s in ServiceSort.values) DropdownMenuItem(value: s, child: Text(s.label))],
              onChanged: (v) => setState(() => _sort = v ?? ServiceSort.name),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.of(context).pop(const ServiceFilter()),
                    child: const Text('Poništi'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: () => Navigator.of(context).pop(
                      ServiceFilter(
                        categoryId: _categoryId,
                        maxPrice: _maxPrice,
                        maxDurationMinutes: _maxDuration,
                        sort: _sort,
                      ),
                    ),
                    child: const Text('Primijeni'),
                  ),
                ),
              ],
            ),
          ],
        ),
      );
}
