import 'dart:typed_data';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../data/models/common.dart';
import '../../data/models/lookup.dart';
import '../../data/models/service.dart';
import '../appointments/appointment_dialogs.dart';

/// Adds a service, or edits one when [service] is given. The picture is uploaded after the
/// service itself is saved, since a new service needs its id first.
class ServiceFormDialog extends ConsumerStatefulWidget {
  const ServiceFormDialog({this.service, super.key});

  final ServiceItem? service;

  @override
  ConsumerState<ServiceFormDialog> createState() => _ServiceFormDialogState();
}

class _ServiceFormDialogState extends ConsumerState<ServiceFormDialog> with ServerFormState {
  static const _maxImageBytes = 5 * 1024 * 1024;

  late final ServiceItem? _s = widget.service;
  late final _name = TextEditingController(text: _s?.name);
  late final _description = TextEditingController(text: _s?.description);
  late final _price = TextEditingController(text: _s?.price.toStringAsFixed(2));
  late final _duration = TextEditingController(text: _s?.durationMinutes.toString());
  late final _intervalKm = TextEditingController(text: _s?.mileageIntervalKm?.toString());
  late final _intervalMonths = TextEditingController(text: _s?.monthsInterval?.toString());
  late final _minAge = TextEditingController(text: _s?.minVehicleAgeYears?.toString());
  late final _maxAge = TextEditingController(text: _s?.maxVehicleAgeYears?.toString());
  late int? _categoryId = _s?.categoryId;
  late bool _active = _s?.isActive ?? true;
  late final Set<int> _tagIds = {...?_s?.tags.map((t) => t.id)};
  late final Set<int> _engineTypeIds = {...?_s?.engineTypes.map((e) => e.id)};
  Uint8List? _image;
  String? _imageName;
  String? _imageError;

  @override
  void dispose() {
    for (final c in [_name, _description, _price, _duration, _intervalKm, _intervalMonths, _minAge, _maxAge]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _pickImage() async {
    final file = await openFile(
      acceptedTypeGroups: const [
        XTypeGroup(label: 'Slike', extensions: ['jpg', 'jpeg', 'png', 'webp']),
      ],
    );
    if (file == null) {
      return;
    }
    final bytes = await file.readAsBytes();
    setState(() {
      if (bytes.length > _maxImageBytes) {
        _imageError = 'Slika može imati najviše 5 MB.';
        return;
      }
      _image = bytes;
      _imageName = file.name;
      _imageError = null;
    });
  }

  int? _optionalInt(TextEditingController c) => c.text.trim().isEmpty ? null : int.parse(c.text.trim());

  Future<void> _save() async {
    final request = ServiceRequest(
      name: _name.text.trim(),
      description: _description.text.trim(),
      categoryId: _categoryId ?? 0,
      price: double.tryParse(_price.text.trim().replaceAll(',', '.')) ?? 0,
      durationMinutes: int.tryParse(_duration.text.trim()) ?? 0,
      mileageIntervalKm: _optionalInt(_intervalKm),
      monthsInterval: _optionalInt(_intervalMonths),
      minVehicleAgeYears: _optionalInt(_minAge),
      maxVehicleAgeYears: _optionalInt(_maxAge),
      isActive: _active,
      tagIds: _tagIds.toList(),
      engineTypeIds: _engineTypeIds.toList(),
    );
    final repository = ref.read(serviceRepositoryProvider);

    final saved = await submit(() async {
      final service = _s == null ? await repository.create(request) : await repository.update(_s.id, request);
      if (_image != null) {
        await repository.uploadImage(service.id, _image!, _imageName!);
      }
    });

    if (saved && mounted) {
      ref.read(referenceVersionProvider.notifier).bump();
      showMessage(
        context,
        _s == null ? 'Usluga „${request.name}" je dodana u katalog.' : 'Izmjene usluge su sačuvane.',
      );
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lookup = ref.watch(lookupProvider).value;

    return ActionDialog(
      title: _s == null ? 'Nova usluga' : 'Uredi uslugu',
      confirmLabel: 'Sačuvaj',
      width: 760,
      busy: busy,
      error: formError,
      onConfirm: lookup == null ? null : _save,
      content: lookup == null ? const Center(child: CircularProgressIndicator()) : _form(lookup),
    );
  }

  Widget _form(Lookup lookup) => Form(
    key: formKey,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Column(
              children: [
                if (_image != null)
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.memory(_image!, width: 140, height: 140, fit: BoxFit.cover),
                  )
                else
                  AuthImage(url: _s?.imageUrl, fallbackIcon: Icons.image_outlined, width: 140, height: 140),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: _pickImage,
                  icon: const Icon(Icons.upload_outlined, size: 18),
                  label: const Text('Izaberi sliku'),
                ),
                if (_imageError != null || serverError('file') != null)
                  SizedBox(
                    width: 140,
                    child: Text(
                      _imageError ?? serverError('file')!,
                      style: const TextStyle(color: AppColors.danger, fontSize: 12),
                    ),
                  ),
              ],
            ),
            const SizedBox(width: 20),
            Expanded(
              child: Column(
                children: [
                  _text(
                    _name,
                    'name',
                    'Naziv',
                    (v) => (v ?? '').trim().length < 2 ? 'Naziv: unesite najmanje 2 znaka.' : null,
                  ),
                  _text(
                    _description,
                    'description',
                    'Opis',
                    (v) => (v ?? '').trim().isEmpty ? 'Opis je obavezno polje.' : null,
                    lines: 3,
                  ),
                  DropdownButtonFormField<int>(
                    initialValue: lookup.categories.any((c) => c.id == _categoryId) ? _categoryId : null,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Kategorija'),
                    items: [for (final c in lookup.categories) DropdownMenuItem(value: c.id, child: Text(c.name))],
                    onChanged: (v) {
                      clearServerError('categoryId');
                      setState(() => _categoryId = v);
                    },
                    validator: (v) => v == null ? 'Izaberite kategoriju.' : null,
                    forceErrorText: serverError('categoryId'),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: _text(_price, 'price', 'Cijena', (v) {
                final price = double.tryParse((v ?? '').trim().replaceAll(',', '.'));
                return price == null || price <= 0 || price > 100000
                    ? 'Cijena mora biti veća od 0 i najviše 100000, npr. 49.90.'
                    : null;
              }),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _text(_duration, 'estimatedDurationMinutes', 'Trajanje (min)', (v) {
                final minutes = int.tryParse((v ?? '').trim());
                return minutes == null || minutes < 5 || minutes > 1440 || minutes % 5 != 0
                    ? 'Trajanje je od 5 do 1440 minuta, u koracima od 5.'
                    : null;
              }),
            ),
          ],
        ),
        Row(
          children: [
            Expanded(
              child: _text(
                _intervalKm,
                'recommendedMileageIntervalKm',
                'Interval (km, nije obavezan)',
                _optionalNumber,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _text(
                _intervalMonths,
                'recommendedMonthsInterval',
                'Interval (mjeseci, nije obavezan)',
                _optionalNumber,
              ),
            ),
          ],
        ),
        Row(
          children: [
            Expanded(child: _text(_minAge, 'minVehicleAgeYears', 'Najmanja starost vozila (god.)', _optionalNumber)),
            const SizedBox(width: 12),
            Expanded(child: _text(_maxAge, 'maxVehicleAgeYears', 'Najveća starost vozila (god.)', _optionalNumber)),
          ],
        ),
        _chips('Oznake', lookup.tags, _tagIds, serverError('tagIds')),
        const SizedBox(height: 12),
        _chips(
          'Tipovi motora (bez izbora: svi motori)',
          lookup.engineTypes,
          _engineTypeIds,
          serverError('engineTypeIds'),
        ),
        const SizedBox(height: 8),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Aktivna'),
          subtitle: const Text('Neaktivna usluga se ne nudi kupcima, ali ostaje na postojećim rezervacijama.'),
          value: _active,
          onChanged: (v) => setState(() => _active = v),
        ),
      ],
    ),
  );

  String? _optionalNumber(String? v) =>
      (v ?? '').trim().isEmpty || int.tryParse(v!.trim()) != null ? null : 'Unesite cijeli broj ili ostavite prazno.';

  Widget _text(
    TextEditingController controller,
    String field,
    String label,
    String? Function(String?) validator, {
    int lines = 1,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: TextFormField(
      controller: controller,
      maxLines: lines,
      decoration: InputDecoration(labelText: label),
      validator: validator,
      forceErrorText: serverError(field),
      onChanged: (_) => clearServerError(field),
    ),
  );

  Widget _chips(String label, List<NamedItem> options, Set<int> selected, String? error) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(label, style: const TextStyle(color: AppColors.textSecondary)),
      const SizedBox(height: 6),
      Wrap(
        spacing: 6,
        runSpacing: 6,
        children: [
          for (final option in options)
            FilterChip(
              label: Text(option.name),
              selected: selected.contains(option.id),
              onSelected: (on) => setState(() => on ? selected.add(option.id) : selected.remove(option.id)),
            ),
        ],
      ),
      if (error != null) Text(error, style: const TextStyle(color: AppColors.danger, fontSize: 12)),
    ],
  );
}
