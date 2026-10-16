import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/recommendation.dart';
import '../../data/models/vehicle.dart';

// Fetched on demand only: every fetch is logged on the server as a set of recommendations
// shown, so background refreshes would fill that log with sets nobody saw.
final _recommendationsProvider = FutureProvider.autoDispose.family<Recommendations, int>(
  (ref, vehicleId) => ref.watch(recommendationRepositoryProvider).forVehicle(vehicleId),
);

/// Services suggested for one of the customer's vehicles, each with the reasons it was chosen.
class RecommendationsScreen extends ConsumerStatefulWidget {
  const RecommendationsScreen({this.vehicleId, super.key});

  final int? vehicleId;

  @override
  ConsumerState<RecommendationsScreen> createState() => _RecommendationsScreenState();
}

class _RecommendationsScreenState extends ConsumerState<RecommendationsScreen> {
  late int? _vehicleId = widget.vehicleId;

  Future<void> _book(RecommendationItem item, int vehicleId) async {
    // Recorded so the recommender learns what was acted on; the booking opens either way.
    try {
      await ref.read(recommendationRepositoryProvider).clicked(item.serviceId, vehicleId);
    } on ApiException catch (error) {
      debugPrint('Klik na preporuku nije zabilježen: ${error.message}');
    }
    if (mounted) {
      context.go('/book?vehicleId=$vehicleId&serviceId=${item.serviceId}');
    }
  }

  Future<void> _markDone(RecommendationItem item, int vehicleId) async {
    final confirmed = await confirm(
      context,
      title: 'Usluga je već obavljena?',
      message: 'Označite da je "${item.serviceName}" već urađena na ovom vozilu, npr. u drugom servisu. '
          'Preporuka za nju počinje se računati od danas i trenutne kilometraže.',
      confirmLabel: 'Obavljeno',
    );
    if (!confirmed || !mounted) {
      return;
    }

    try {
      await ref.read(recommendationRepositoryProvider).markDone(item.serviceId, vehicleId);
      ref.invalidate(_recommendationsProvider(vehicleId));
      ref.invalidate(vehiclesProvider);
      if (mounted) {
        showMessage(context, 'Zabilježeno. Preporuke su ažurirane.');
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final vehicles = ref.watch(vehiclesProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Preporuke'),
        actions: [
          if (_vehicleId case final id?)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: TextButton.icon(
                onPressed: () => ref.invalidate(_recommendationsProvider(id)),
                icon: const Icon(Icons.refresh),
                label: const Text('Osvježi'),
              ),
            ),
        ],
      ),
      body: AsyncBody<List<Vehicle>>(
        value: vehicles,
        onRetry: () => ref.invalidate(vehiclesProvider),
        builder: (list) {
          if (list.isEmpty) {
            return EmptyView(
              icon: Icons.directions_car_outlined,
              message: 'Preporuke se prave za konkretno vozilo. Dodajte vozilo da biste ih vidjeli.',
              action: FilledButton.icon(
                style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                onPressed: () => context.push('/vehicles/new'),
                icon: const Icon(Icons.add),
                label: const Text('Dodaj vozilo'),
              ),
            );
          }

          final vehicleId = list.any((v) => v.id == _vehicleId) ? _vehicleId! : list.first.id;
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (list.length > 1)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
                  child: DropdownButtonFormField<int>(
                    initialValue: vehicleId,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Vozilo',
                      prefixIcon: Icon(Icons.directions_car_outlined),
                    ),
                    items: [
                      for (final v in list)
                        DropdownMenuItem(value: v.id, child: Text('${v.displayName} (${v.registrationPlate})')),
                    ],
                    onChanged: (id) => setState(() => _vehicleId = id),
                  ),
                ),
              Expanded(child: _RecommendationList(vehicleId: vehicleId, onBook: _book, onDone: _markDone)),
            ],
          );
        },
      ),
    );
  }
}

class _RecommendationList extends ConsumerWidget {
  const _RecommendationList({required this.vehicleId, required this.onBook, required this.onDone});

  final int vehicleId;
  final void Function(RecommendationItem item, int vehicleId) onBook;
  final void Function(RecommendationItem item, int vehicleId) onDone;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final recommendations = ref.watch(_recommendationsProvider(vehicleId));

    return AsyncBody<Recommendations>(
      value: recommendations,
      onRetry: () => ref.invalidate(_recommendationsProvider(vehicleId)),
      builder: (data) => RefreshIndicator(
        onRefresh: () => ref.refresh(_recommendationsProvider(vehicleId).future),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
          children: [
            _VehicleHeader(data: data),
            const SizedBox(height: 14),
            if (data.items.isEmpty)
              const EmptyView(
                icon: Icons.thumb_up_alt_outlined,
                message: 'Trenutno nema preporuka za ovo vozilo. Sve preporučene usluge su obavljene ili zakazane.',
              ),
            for (final item in data.items)
              Padding(
                padding: const EdgeInsets.only(bottom: 14),
                child: _RecommendationCard(
                  item: item,
                  onBook: () => onBook(item, vehicleId),
                  onDone: () => onDone(item, vehicleId),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _VehicleHeader extends StatelessWidget {
  const _VehicleHeader({required this.data});

  final Recommendations data;

  @override
  Widget build(BuildContext context) {
    final vehicle = data.vehicle;
    final services = vehicle.completedServiceCount;

    return SectionCard(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(vehicle.display, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
                const SizedBox(height: 6),
                Text(
                  [
                    Fmt.km(vehicle.mileageKm),
                    '${vehicle.productionYear}.',
                    vehicle.engineTypeName.toLowerCase(),
                    '${vehicle.ageYears} god.',
                    '$services ${services == 1 ? 'servis' : 'servisa'}',
                  ].join(' | '),
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 13),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Pill(label: data.algorithm, color: AppColors.accent, icon: Icons.hub_outlined),
        ],
      ),
    );
  }
}

class _RecommendationCard extends StatelessWidget {
  const _RecommendationCard({required this.item, required this.onBook, required this.onDone});

  final RecommendationItem item;
  final VoidCallback onBook;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) {
    final accent = item.isUrgent ? AppColors.urgent : AppColors.accent;
    final interval = Fmt.interval(km: item.mileageIntervalKm, months: item.monthsInterval);

    return Container(
      decoration: BoxDecoration(
        color: item.isUrgent ? const Color(0xFF1C1612) : AppColors.surface,
        borderRadius: BorderRadius.circular(AppTheme.radius),
        border: Border(left: BorderSide(color: accent, width: 4)),
      ),
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(item.serviceName, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
              ),
              Pill(
                label: item.isUrgent ? 'Hitno' : 'Planirano',
                color: accent,
                icon: item.isUrgent ? Icons.local_fire_department_outlined : Icons.event_outlined,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              const Icon(Icons.sell_outlined, size: 14, color: AppColors.textSecondary),
              const SizedBox(width: 4),
              Expanded(
                child: Text(
                  [item.categoryName, if (interval.isNotEmpty) interval].join(' | '),
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          // Why this service: the reasons the server gives, as the mockup shows them.
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: AppColors.surfaceRaised, borderRadius: BorderRadius.circular(10)),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (final reason in item.reasons)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Icon(Icons.lightbulb_outline, size: 15, color: AppColors.warning),
                        const SizedBox(width: 6),
                        Expanded(child: Text(reason, style: const TextStyle(fontSize: 13, height: 1.3))),
                      ],
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(minimumSize: const Size(0, 40), shape: const StadiumBorder()),
                  onPressed: onBook,
                  icon: const Icon(Icons.event_available, size: 18),
                  label: const Text('Zakaži'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(0, 40),
                    shape: const StadiumBorder(),
                    backgroundColor: AppColors.success,
                  ),
                  onPressed: onDone,
                  icon: const Icon(Icons.check, size: 18),
                  label: const Text('Obavljeno'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
