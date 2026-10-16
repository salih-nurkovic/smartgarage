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
import '../../core/widgets/dialogs.dart';
import '../../data/models/service.dart';
import '../../data/models/vehicle.dart';
import 'slot_picker.dart';

/// The whole active catalogue fits in one page; the booking list needs all of it.
final bookableServicesProvider = FutureProvider.autoDispose<List<ServiceItem>>((ref) async {
  final page = await ref.watch(catalogRepositoryProvider).services(const ServiceFilter(), pageSize: 100);
  return page.items;
});

/// Booking in the four steps of the approved mockups: vehicle, services, date and time,
/// confirmation.
class BookingScreen extends ConsumerStatefulWidget {
  const BookingScreen({this.vehicleId, this.serviceId, super.key});

  final int? vehicleId;
  final int? serviceId;

  @override
  ConsumerState<BookingScreen> createState() => _BookingScreenState();
}

class _BookingScreenState extends ConsumerState<BookingScreen> {
  static const _maxServices = 10;
  static const _stepTitles = ['Izaberite vozilo', 'Izaberite usluge', 'Datum i vrijeme', 'Potvrda rezervacije'];

  final _notes = TextEditingController();
  late int _step = widget.vehicleId == null ? 0 : 1;
  late int? _vehicleId = widget.vehicleId;
  late final Set<int> _serviceIds = {?widget.serviceId};
  DateTime? _date;
  DateTime? _slot;
  var _sending = false;
  String? _notesError;

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  void _reset() => setState(() {
        _step = 0;
        _vehicleId = null;
        _serviceIds.clear();
        _date = null;
        _slot = null;
        _notes.clear();
      });

  Future<void> _confirm(Vehicle vehicle, List<ServiceItem> services, MoneyFormat money) async {
    final notesError = Validators.maxLength(_notes.text, Validators.notesMaxLength, 'Napomena');
    setState(() => _notesError = notesError);
    if (notesError != null) {
      return;
    }

    final total = services.fold<double>(0, (sum, s) => sum + s.price);
    final confirmed = await confirm(
      context,
      title: 'Potvrditi rezervaciju?',
      message: '${vehicle.displayName}, ${Fmt.dayMonth(_slot!)} u ${Fmt.time(_slot!)}.\n'
          'Ukupno ${money(total)}, plaća se nakon završenog servisa.',
      confirmLabel: 'Potvrdi',
    );
    if (!confirmed || !mounted) {
      return;
    }

    setState(() => _sending = true);
    try {
      final created = await ref.read(appointmentRepositoryProvider).create(
            vehicleId: vehicle.id,
            serviceIds: [for (final s in services) s.id],
            startUtc: _slot!,
            notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
          );
      if (!mounted) {
        return;
      }
      ref.read(appointmentsVersionProvider.notifier).bump();
      ref.invalidate(vehiclesProvider);
      showMessage(context, 'Rezervacija ${created.number} je poslana i čeka potvrdu servisa.');
      _reset();
      unawaited(context.push('/appointments/${created.id}'));
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      showMessage(context, error.message, error: true);
      // The slot was taken in the meantime: back to the slots, which are reloaded.
      if (error.statusCode == 409) {
        ref.read(appointmentsVersionProvider.notifier).bump();
        setState(() {
          _step = 2;
          _slot = null;
        });
      }
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final vehicles = ref.watch(vehiclesProvider);
    final services = ref.watch(bookableServicesProvider);

    return Scaffold(
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            PageHeader(title: 'Zakazivanje termina', subtitle: 'Korak ${_step + 1} od 4'),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: LinearProgressIndicator(
                  value: (_step + 1) / 4,
                  minHeight: 6,
                  backgroundColor: AppColors.surfaceRaised,
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 20, 16, 12),
              child: Text(_stepTitles[_step], style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            ),
            Expanded(
              child: AsyncBody<List<Vehicle>>(
                value: vehicles,
                onRetry: () => ref.invalidate(vehiclesProvider),
                builder: (vehicleList) => AsyncBody<List<ServiceItem>>(
                  value: services,
                  onRetry: () => ref.invalidate(bookableServicesProvider),
                  builder: (serviceList) => _stepBody(vehicleList, serviceList),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _stepBody(List<Vehicle> vehicles, List<ServiceItem> catalogue) {
    final money = ref.watch(moneyProvider);
    final vehicle = vehicles.where((v) => v.id == _vehicleId).firstOrNull;
    final suitable = vehicle == null ? catalogue : catalogue.where((s) => s.suitsEngine(vehicle.engineTypeId)).toList();
    final chosen = suitable.where((s) => _serviceIds.contains(s.id)).toList();

    // A step whose input is missing - a vehicle from a link that no longer exists, or a slot
    // cleared after a conflict - falls back to the step that provides it.
    if (vehicle == null) {
      return _vehicleStep(vehicles);
    }
    if (_step >= 2 && chosen.isEmpty) {
      return _servicesStep(vehicle, suitable, chosen, money);
    }
    if (_step == 3 && _slot == null) {
      return _dateStep(chosen);
    }

    return switch (_step) {
      0 => _vehicleStep(vehicles),
      1 => _servicesStep(vehicle, suitable, chosen, money),
      2 => _dateStep(chosen),
      _ => _summaryStep(vehicle, chosen, money),
    };
  }

  Widget _buttons({required bool canContinue, required VoidCallback onContinue, String label = 'Nastavi', bool showBack = true}) =>
      Padding(
        padding: const EdgeInsets.only(top: 8),
        child: Row(
          children: [
            if (showBack) ...[
              Expanded(
                child: OutlinedButton(
                  onPressed: _sending ? null : () => setState(() => _step--),
                  child: const Text('Nazad'),
                ),
              ),
              const SizedBox(width: 12),
            ],
            Expanded(
              child: BusyButton(label: label, busy: _sending, onPressed: canContinue ? onContinue : null),
            ),
          ],
        ),
      );

  Widget _vehicleStep(List<Vehicle> vehicles) {
    if (vehicles.isEmpty) {
      return EmptyView(
        icon: Icons.directions_car_outlined,
        message: 'Za zakazivanje vam treba vozilo. Dodajte ga, pa se vratite ovdje.',
        action: FilledButton.icon(
          style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
          onPressed: () => context.push('/vehicles/new'),
          icon: const Icon(Icons.add),
          label: const Text('Dodaj vozilo'),
        ),
      );
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
      children: [
        for (final vehicle in vehicles)
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: _SelectableCard(
              selected: vehicle.id == _vehicleId,
              onTap: () => setState(() => _vehicleId = vehicle.id),
              leading: const IconTile(icon: Icons.directions_car_outlined),
              title: vehicle.displayName,
              subtitle: '${vehicle.registrationPlate} • ${vehicle.engineTypeName}',
            ),
          ),
        TextButton.icon(
          onPressed: () => context.push('/vehicles/new'),
          icon: const Icon(Icons.add),
          label: const Text('Dodaj drugo vozilo'),
        ),
        _buttons(
          showBack: false,
          canContinue: _vehicleId != null,
          onContinue: () => setState(() => _step = 1),
        ),
      ],
    );
  }

  Widget _servicesStep(Vehicle vehicle, List<ServiceItem> suitable, List<ServiceItem> chosen, MoneyFormat money) {
    final total = chosen.fold<double>(0, (sum, s) => sum + s.price);
    final minutes = chosen.fold<int>(0, (sum, s) => sum + s.durationMinutes);
    final tooMany = chosen.length > _maxServices;

    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            children: [
              Text(
                'Prikazane su usluge koje odgovaraju vozilu ${vehicle.displayName} (${vehicle.engineTypeName.toLowerCase()}). '
                'Možete izabrati više usluga.',
                style: const TextStyle(color: AppColors.textSecondary),
              ),
              const SizedBox(height: 12),
              for (final service in suitable)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: _SelectableCard(
                    selected: _serviceIds.contains(service.id),
                    onTap: () => setState(() {
                      if (!_serviceIds.remove(service.id)) {
                        _serviceIds.add(service.id);
                      }
                      _slot = null;
                    }),
                    title: service.name,
                    subtitle: '${money(service.price)} • ${Fmt.duration(service.durationMinutes)}',
                    subtitleColor: AppColors.accent,
                  ),
                ),
            ],
          ),
        ),
        Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.border))),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                chosen.isEmpty
                    ? 'Izaberite najmanje jednu uslugu.'
                    : 'Izabrano: ${chosen.length}  •  ${Fmt.duration(minutes)}  •  ${money(total)}',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              if (tooMany) BlockedReason('Jedna rezervacija može imati najviše $_maxServices usluga.'),
              _buttons(
                canContinue: chosen.isNotEmpty && !tooMany,
                onContinue: () => setState(() => _step = 2),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _dateStep(List<ServiceItem> chosen) => ListView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        children: [
          SlotPicker(
            serviceIds: [for (final s in chosen) s.id],
            date: _date,
            selected: _slot,
            onDateChanged: (d) => setState(() => _date = d),
            onSlotChanged: (s) => setState(() => _slot = s),
          ),
          const SizedBox(height: 20),
          _buttons(canContinue: _slot != null, onContinue: () => setState(() => _step = 3)),
        ],
      );

  Widget _summaryStep(Vehicle vehicle, List<ServiceItem> chosen, MoneyFormat money) {
    final total = chosen.fold<double>(0, (sum, s) => sum + s.price);
    final minutes = chosen.fold<int>(0, (sum, s) => sum + s.durationMinutes);
    final slot = _slot!;

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
      children: [
        SectionCard(
          child: Column(
            children: [
              KeyValueRow(label: 'Vozilo:', value: vehicle.displayName),
              KeyValueRow(label: chosen.length == 1 ? 'Usluga:' : 'Usluge:', value: chosen.map((s) => s.name).join('\n')),
              KeyValueRow(label: 'Datum:', value: Fmt.date(slot)),
              KeyValueRow(label: 'Vrijeme:', value: Fmt.time(slot)),
              KeyValueRow(label: 'Trajanje:', value: Fmt.duration(minutes)),
              const Divider(height: 24),
              KeyValueRow(
                label: 'Ukupno:',
                value: money(total),
                valueStyle: const TextStyle(color: AppColors.accent, fontSize: 22, fontWeight: FontWeight.w800),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _notes,
          maxLines: 3,
          maxLength: Validators.notesMaxLength,
          decoration: InputDecoration(
            labelText: 'Napomena za servis (nije obavezna)',
            hintText: 'npr. čuje se zvuk pri kočenju',
            alignLabelWithHint: true,
            errorText: _notesError,
          ),
        ),
        const Text(
          'Plaćanje je moguće nakon završenog servisa, iz detalja rezervacije.',
          style: TextStyle(color: AppColors.textSecondary, fontSize: 13),
        ),
        const SizedBox(height: 16),
        _buttons(label: 'Potvrdi', canContinue: true, onContinue: () => _confirm(vehicle, chosen, money)),
      ],
    );
  }
}

class _SelectableCard extends StatelessWidget {
  const _SelectableCard({
    required this.selected,
    required this.onTap,
    required this.title,
    required this.subtitle,
    this.leading,
    this.subtitleColor = AppColors.textSecondary,
  });

  final bool selected;
  final VoidCallback onTap;
  final String title;
  final String subtitle;
  final Widget? leading;
  final Color subtitleColor;

  @override
  Widget build(BuildContext context) => Material(
        color: AppColors.surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          side: BorderSide(color: selected ? AppColors.accent : AppColors.border, width: selected ? 1.5 : 1),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                if (leading != null) ...[leading!, const SizedBox(width: 14)],
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                      const SizedBox(height: 3),
                      Text(subtitle, style: TextStyle(color: subtitleColor, fontWeight: FontWeight.w600, fontSize: 13)),
                    ],
                  ),
                ),
                Icon(
                  selected ? Icons.check_circle : Icons.radio_button_unchecked,
                  color: selected ? AppColors.accent : AppColors.textSecondary,
                ),
              ],
            ),
          ),
        ),
      );
}
