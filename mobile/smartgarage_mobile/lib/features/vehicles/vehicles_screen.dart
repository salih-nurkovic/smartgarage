import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../data/models/vehicle.dart';

class VehiclesScreen extends ConsumerWidget {
  const VehiclesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vehicles = ref.watch(vehiclesProvider);
    final lookup = ref.watch(lookupProvider).value;

    // A vehicle needs a brand and an engine type from the database; without them the form
    // could not be completed, so it does not open.
    final canAdd = lookup != null && lookup.brands.isNotEmpty && lookup.engineTypes.isNotEmpty;

    return Scaffold(
      appBar: AppBar(title: const Text('Moja vozila')),
      body: AsyncBody(
        value: vehicles,
        onRetry: () => ref.invalidate(vehiclesProvider),
        builder: (items) => RefreshIndicator(
          onRefresh: () => ref.refresh(vehiclesProvider.future),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
            children: [
              const Text('Upravljajte svojim vozilima', style: TextStyle(color: AppColors.textSecondary)),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: canAdd ? () => context.push('/vehicles/new') : null,
                icon: const Icon(Icons.add),
                label: const Text('Dodaj novo vozilo'),
              ),
              if (!canAdd && lookup != null)
                const BlockedReason('Dodavanje vozila trenutno nije moguće jer servis još nije unio marke i tipove motora.'),
              const SizedBox(height: 16),
              if (items.isEmpty)
                const EmptyView(
                  icon: Icons.directions_car_outlined,
                  message: 'Još niste dodali nijedno vozilo. Dodajte vozilo da biste mogli zakazati servis.',
                ),
              for (final vehicle in items)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: _VehicleCard(vehicle: vehicle),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({required this.vehicle});

  final Vehicle vehicle;

  @override
  Widget build(BuildContext context) {
    final nextService = vehicle.nextServiceDueAtUtc;
    final lastService = vehicle.lastServiceAtUtc;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                const IconTile(icon: Icons.directions_car_outlined, size: 52),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(vehicle.displayName, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
                      Text(
                        '${vehicle.productionYear} • ${vehicle.registrationPlate} • ${vehicle.engineTypeName}',
                        style: const TextStyle(color: AppColors.textSecondary),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Uredi vozilo',
                  onPressed: () => context.push('/vehicles/${vehicle.id}/edit', extra: vehicle),
                  icon: const Icon(Icons.settings_outlined),
                ),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: _Stat(
                    icon: Icons.speed,
                    color: AppColors.accent,
                    label: 'Kilometraža',
                    value: Fmt.km(vehicle.currentMileageKm),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _Stat(
                    icon: Icons.event_outlined,
                    color: AppColors.success,
                    label: 'Zadnji servis',
                    value: lastService == null ? 'Nema podataka' : Fmt.date(lastService),
                  ),
                ),
              ],
            ),
            if (nextService != null) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                decoration: BoxDecoration(
                  color: AppColors.warningSurface,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: AppColors.warning.withValues(alpha: 0.5)),
                ),
                child: Text(
                  'Sljedeći servis: ${Fmt.date(nextService)}',
                  style: const TextStyle(color: AppColors.warning, fontWeight: FontWeight.w700),
                ),
              ),
            ],
            const SizedBox(height: 12),
            _ActionRow(
              label: 'Zakaži servis',
              onTap: () => context.go('/book?vehicleId=${vehicle.id}'),
            ),
            const SizedBox(height: 8),
            _ActionRow(
              label: 'Preporuke za ovo vozilo',
              onTap: () => context.push('/recommendations?vehicleId=${vehicle.id}'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.icon, required this.color, required this.label, required this.value});

  final IconData icon;
  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: AppColors.surfaceRaised, borderRadius: BorderRadius.circular(10)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 15, color: color),
                const SizedBox(width: 6),
                Text(label, style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
            const SizedBox(height: 6),
            Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
          ],
        ),
      );
}

class _ActionRow extends StatelessWidget {
  const _ActionRow({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Material(
        color: AppColors.surfaceRaised,
        borderRadius: BorderRadius.circular(10),
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
            child: Row(
              children: [
                Expanded(child: Text(label, style: const TextStyle(fontWeight: FontWeight.w700))),
                const Icon(Icons.chevron_right),
              ],
            ),
          ),
        ),
      );
}
