import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../data/models/common.dart';
import '../../data/models/lookup.dart';
import '../../data/models/vehicle.dart';

/// Adds a vehicle, or edits one when [vehicle] is given.
class VehicleFormScreen extends ConsumerStatefulWidget {
  const VehicleFormScreen({this.vehicle, super.key});

  final Vehicle? vehicle;

  @override
  ConsumerState<VehicleFormScreen> createState() => _VehicleFormScreenState();
}

class _VehicleFormScreenState extends ConsumerState<VehicleFormScreen> with ServerFormState {
  late final _plate = TextEditingController(text: widget.vehicle?.registrationPlate);
  late final _vin = TextEditingController(text: widget.vehicle?.vin);
  late final _mileage = TextEditingController(text: widget.vehicle?.currentMileageKm.toString());
  late int? _brandId = widget.vehicle?.brandId;
  late int? _modelId = widget.vehicle?.modelId;
  late int? _engineTypeId = widget.vehicle?.engineTypeId;
  late int? _year = widget.vehicle?.productionYear;

  bool get _editing => widget.vehicle != null;

  @override
  void dispose() {
    _plate.dispose();
    _vin.dispose();
    _mileage.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final request = VehicleRequest(
      brandId: _brandId!,
      modelId: _modelId!,
      engineTypeId: _engineTypeId!,
      productionYear: _year!,
      registrationPlate: _plate.text.trim().toUpperCase(),
      vin: _vin.text.trim().toUpperCase(),
      currentMileageKm: int.parse(_mileage.text.trim()),
    );
    final repository = ref.read(vehicleRepositoryProvider);

    final saved = await submit(() async {
      if (_editing) {
        await repository.update(widget.vehicle!.id, request);
      } else {
        await repository.create(request);
      }
    });

    if (saved && mounted) {
      ref.invalidate(vehiclesProvider);
      showMessage(context, _editing ? 'Izmjene vozila su sačuvane.' : 'Vozilo je dodano. Sada mu možete zakazati servis.');
      Navigator.of(context).pop();
    }
  }

  Future<void> _delete() async {
    final vehicle = widget.vehicle!;
    final confirmed = await confirm(
      context,
      title: 'Obrisati vozilo?',
      message: '${vehicle.displayName} (${vehicle.registrationPlate}) će biti uklonjeno iz vaših vozila.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!confirmed || !mounted) {
      return;
    }

    try {
      await ref.read(vehicleRepositoryProvider).delete(vehicle.id);
      ref.invalidate(vehiclesProvider);
      if (mounted) {
        showMessage(context, 'Vozilo je obrisano.');
        Navigator.of(context).pop();
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final lookup = ref.watch(lookupProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_editing ? 'Uredi vozilo' : 'Novo vozilo')),
      body: AsyncBody<Lookup>(
        value: lookup,
        onRetry: () => ref.invalidate(lookupProvider),
        builder: _form,
      ),
    );
  }

  Widget _form(Lookup lookup) {
    final brand = lookup.brands.where((b) => b.id == _brandId).firstOrNull;
    final maxYear = DateTime.now().year + 1;
    final hasAppointments = (widget.vehicle?.appointmentCount ?? 0) > 0;

    return Form(
      key: formKey,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _dropdown(
            field: 'brandId',
            label: 'Marka',
            icon: Icons.directions_car_outlined,
            value: _brandId,
            items: [for (final b in lookup.brands) NamedItem(b.id, b.name)],
            onChanged: (id) => setState(() {
              _brandId = id;
              _modelId = null;
            }),
          ),
          _dropdown(
            field: 'modelId',
            label: 'Model',
            icon: Icons.car_repair_outlined,
            value: _modelId,
            items: brand?.models ?? const [],
            hint: brand == null ? 'Prvo izaberite marku' : null,
            onChanged: (id) => setState(() => _modelId = id),
          ),
          _dropdown(
            field: 'engineTypeId',
            label: 'Tip motora',
            icon: Icons.local_gas_station_outlined,
            value: _engineTypeId,
            items: lookup.engineTypes,
            onChanged: (id) => setState(() => _engineTypeId = id),
          ),
          _dropdown(
            field: 'productionYear',
            label: 'Godina proizvodnje',
            icon: Icons.event_outlined,
            value: _year,
            items: [for (var y = maxYear; y >= Validators.vehicleYearMin; y--) NamedItem(y, '$y.')],
            onChanged: (year) => setState(() => _year = year),
          ),
          _text(
            controller: _plate,
            field: 'registrationPlate',
            label: 'Registarska oznaka',
            icon: Icons.pin_outlined,
            helper: 'npr. A12-K-345',
            validator: Validators.plate,
          ),
          _text(
            controller: _vin,
            field: 'vin',
            label: 'Broj šasije (VIN)',
            icon: Icons.qr_code_2,
            helper: '17 znakova, nalazi se u saobraćajnoj dozvoli',
            validator: Validators.vin,
            maxLength: Validators.vinLength,
          ),
          _text(
            controller: _mileage,
            field: 'currentMileageKm',
            label: 'Kilometraža (km)',
            icon: Icons.speed,
            keyboard: TextInputType.number,
            helper: _editing ? 'Kilometraža se može samo povećati.' : null,
            validator: (v) => Validators.mileage(v, minimum: widget.vehicle?.currentMileageKm ?? 0),
          ),
          FormErrorText(formError),
          BusyButton(label: _editing ? 'Sačuvaj izmjene' : 'Dodaj vozilo', busy: busy, onPressed: _save),
          if (_editing) ...[
            const SizedBox(height: 12),
            OutlinedButton.icon(
              style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
              onPressed: busy || hasAppointments ? null : _delete,
              icon: const Icon(Icons.delete_outline),
              label: const Text('Obriši vozilo'),
            ),
            if (hasAppointments)
              const BlockedReason(
                'Vozilo se ne može obrisati jer ima rezervacije. Historija servisa mora ostati sačuvana.',
              ),
          ],
        ],
      ),
    );
  }

  Widget _dropdown({
    required String field,
    required String label,
    required IconData icon,
    required int? value,
    required List<NamedItem> items,
    required ValueChanged<int?> onChanged,
    String? hint,
  }) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: DropdownButtonFormField<int>(
          key: ValueKey('$field-${items.length}-$value'),
          initialValue: items.any((i) => i.id == value) ? value : null,
          isExpanded: true,
          decoration: InputDecoration(labelText: label, prefixIcon: Icon(icon)),
          hint: hint == null ? null : Text(hint),
          items: [for (final item in items) DropdownMenuItem(value: item.id, child: Text(item.name))],
          onChanged: items.isEmpty
              ? null
              : (id) {
                  clearServerError(field);
                  onChanged(id);
                },
          validator: (v) => v == null ? '$label je obavezno polje.' : null,
          forceErrorText: serverError(field),
        ),
      );

  Widget _text({
    required TextEditingController controller,
    required String field,
    required String label,
    required IconData icon,
    required String? Function(String?) validator,
    String? helper,
    TextInputType? keyboard,
    int? maxLength,
  }) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: TextFormField(
          controller: controller,
          keyboardType: keyboard,
          maxLength: maxLength,
          textCapitalization: TextCapitalization.characters,
          decoration: InputDecoration(labelText: label, helperText: helper, prefixIcon: Icon(icon)),
          validator: validator,
          forceErrorText: serverError(field),
          onChanged: (_) => clearServerError(field),
        ),
      );
}
