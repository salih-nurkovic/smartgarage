import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/reference.dart';
import '../appointments/appointment_dialogs.dart';

enum FieldKind { text, number, brand }

class RefField {
  const RefField(this.key, this.label, this.kind, {this.validator, this.helper});

  final String key;
  final String label;
  final FieldKind kind;
  final String? Function(String?)? validator;
  final String? helper;
}

/// One reference table: where it lives, what a row holds, and what makes a row impossible to
/// delete (the count of records that use it).
class RefTable {
  const RefTable({
    required this.title,
    required this.singular,
    required this.path,
    required this.fields,
    required this.usageKey,
    required this.usageLabel,
    this.extraColumn,
  });

  final String title;
  final String singular;
  final String path;
  final List<RefField> fields;
  final String usageKey;
  final String usageLabel;
  final (String label, String key)? extraColumn;
}

String? _requiredName(String? v) {
  final length = (v ?? '').trim().length;
  return length < 2 || length > 80 ? 'Naziv: unesite između 2 i 80 znakova.' : null;
}

final _slug = RegExp(r'^[a-z0-9]+(-[a-z0-9]+)*$');
final _code = RegExp(r'^[A-Za-z0-9_]{2,20}$');

final referenceTables = [
  RefTable(
    title: 'Kategorije usluga',
    singular: 'kategoriju',
    path: '/api/service-categories',
    usageKey: 'serviceCount',
    usageLabel: 'usluga',
    fields: [
      const RefField('name', 'Naziv', FieldKind.text, validator: _requiredName),
      RefField(
        'description',
        'Opis (nije obavezan)',
        FieldKind.text,
        validator: (v) => (v ?? '').length > 500 ? 'Opis može imati najviše 500 znakova.' : null,
      ),
      RefField(
        'displayOrder',
        'Redoslijed prikaza',
        FieldKind.number,
        helper: 'Manji broj se prikazuje prije.',
        validator: (v) {
          final n = int.tryParse((v ?? '').trim());
          return n == null || n < 0 || n > 1000 ? 'Redoslijed je cijeli broj od 0 do 1000.' : null;
        },
      ),
    ],
    extraColumn: ('Redoslijed', 'displayOrder'),
  ),
  RefTable(
    title: 'Oznake',
    singular: 'oznaku',
    path: '/api/service-tags',
    usageKey: 'serviceCount',
    usageLabel: 'usluga',
    fields: [
      const RefField('name', 'Naziv', FieldKind.text, validator: _requiredName),
      RefField(
        'slug',
        'Slug',
        FieldKind.text,
        helper: 'Mala slova, cifre i crtice, npr. high-mileage.',
        validator: (v) =>
            _slug.hasMatch((v ?? '').trim()) ? null : 'Slug ima samo mala slova, cifre i crtice, npr. high-mileage.',
      ),
    ],
    extraColumn: ('Slug', 'slug'),
  ),
  const RefTable(
    title: 'Marke vozila',
    singular: 'marku',
    path: '/api/vehicle-brands',
    usageKey: 'modelCount',
    usageLabel: 'modela',
    fields: [RefField('name', 'Naziv', FieldKind.text, validator: _requiredName)],
  ),
  const RefTable(
    title: 'Modeli vozila',
    singular: 'model',
    path: '/api/vehicle-models',
    usageKey: 'vehicleCount',
    usageLabel: 'vozila',
    fields: [
      RefField('brandId', 'Marka', FieldKind.brand),
      RefField('name', 'Naziv', FieldKind.text, validator: _requiredName),
    ],
    extraColumn: ('Marka', 'brandName'),
  ),
  RefTable(
    title: 'Tipovi motora',
    singular: 'tip motora',
    path: '/api/engine-types',
    usageKey: 'vehicleCount',
    usageLabel: 'vozila',
    fields: [
      const RefField('name', 'Naziv', FieldKind.text, validator: _requiredName),
      RefField(
        'code',
        'Kod',
        FieldKind.text,
        helper: 'Koristi ga sistem preporuka, npr. diesel.',
        validator: (v) =>
            _code.hasMatch((v ?? '').trim()) ? null : 'Kod ima 2-20 znakova: slova, cifre ili donja crta.',
      ),
    ],
    extraColumn: ('Kod', 'code'),
  ),
];

/// Every reference table, one tab each, with create, edit and delete.
class ReferenceScreen extends StatelessWidget {
  const ReferenceScreen({super.key});

  @override
  Widget build(BuildContext context) => DefaultTabController(
    length: referenceTables.length,
    child: ScreenScaffold(
      title: 'Šifarnici',
      subtitle: 'Referentni podaci koje koriste usluge, vozila i sistem preporuka',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [for (final table in referenceTables) Tab(text: table.title)],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: TabBarView(children: [for (final table in referenceTables) _TableTab(table: table)]),
          ),
        ],
      ),
    ),
  );
}

class _TableTab extends ConsumerStatefulWidget {
  const _TableTab({required this.table});

  final RefTable table;

  @override
  ConsumerState<_TableTab> createState() => _TableTabState();
}

class _TableTabState extends ConsumerState<_TableTab> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  int? _brandId;
  var _version = 0;

  RefTable get _t => widget.table;

  bool get _isModels => _t.fields.any((f) => f.kind == FieldKind.brand);

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _changed() {
    setState(() => _version++);
    ref.read(referenceVersionProvider.notifier).bump();
  }

  Future<void> _edit([ReferenceItem? item]) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => _ReferenceFormDialog(table: _t, item: item),
    );
    if (saved ?? false) {
      _changed();
    }
  }

  Future<void> _delete(ReferenceItem item) async {
    final ok = await confirm(
      context,
      title: 'Obrisati ${_t.singular}?',
      message: '„${item.name}" bit će obrisan(a).',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!ok || !mounted) {
      return;
    }
    try {
      await ref.read(referenceRepositoryProvider).delete(_t.path, item.id);
      if (mounted) {
        showMessage(context, '„${item.name}" je obrisan(a).');
        _changed();
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(referenceRepositoryProvider);
    final brands = ref.watch(lookupProvider).value?.brands ?? const [];
    final extra = _t.extraColumn;

    // A model belongs to a brand; with no brands the form could not be completed.
    final canAdd = !_isModels || brands.isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _search,
                decoration: InputDecoration(
                  hintText: 'Pretraži: ${_t.title.toLowerCase()}...',
                  prefixIcon: const Icon(Icons.search),
                ),
                onChanged: (value) {
                  _debounce?.cancel();
                  _debounce = Timer(const Duration(milliseconds: 400), () => setState(() => _term = value));
                },
              ),
            ),
            if (_isModels) ...[
              const SizedBox(width: 12),
              SizedBox(
                width: 220,
                child: DropdownButtonFormField<int?>(
                  initialValue: _brandId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Marka'),
                  items: [
                    const DropdownMenuItem(child: Text('Sve marke')),
                    for (final b in brands) DropdownMenuItem(value: b.id, child: Text(b.name)),
                  ],
                  onChanged: (v) => setState(() => _brandId = v),
                ),
              ),
            ],
            const SizedBox(width: 12),
            GuardedButton(
              label: 'Dodaj ${_t.singular}',
              icon: Icons.add,
              filled: true,
              onPressed: canAdd ? _edit : null,
              disabledReason: 'Prvo dodajte barem jednu marku vozila.',
            ),
          ],
        ),
        const SizedBox(height: 16),
        Expanded(
          child: PagedView<ReferenceItem>(
            reloadKey: (_term, _brandId, _version),
            fetch: (page) => repository.list(_t.path, search: _term, filters: {'brandId': _brandId}, page: page),
            emptyMessage: 'Nema zapisa.',
            builder: (context, items, _) => SimpleTable<ReferenceItem>(
              items: items,
              onTap: _edit,
              columns: [
                TableColumn('Naziv', (i) => Text(i.name, style: const TextStyle(fontWeight: FontWeight.w700)), flex: 2),
                if (extra != null) TableColumn(extra.$1, (i) => Text('${i[extra.$2] ?? '—'}')),
                TableColumn('Koristi se', (i) => Text('${i[_t.usageKey]} ${_t.usageLabel}')),
                TableColumn(
                  'Status',
                  (i) => Align(
                    alignment: Alignment.centerLeft,
                    child: Pill(
                      label: i.isActive ? 'Aktivno' : 'Neaktivno',
                      color: i.isActive ? AppColors.success : AppColors.textSecondary,
                    ),
                  ),
                ),
                TableColumn('', (i) {
                  final used = (i[_t.usageKey] as int? ?? 0) > 0;
                  return Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      IconButton(tooltip: 'Uredi', onPressed: () => _edit(i), icon: const Icon(Icons.edit_outlined)),
                      Tooltip(
                        message: used
                            ? 'Koristi ga ${i[_t.usageKey]} ${_t.usageLabel}, pa se ne može obrisati. Označite ga kao neaktivnog.'
                            : 'Obriši',
                        child: IconButton(
                          onPressed: used ? null : () => _delete(i),
                          icon: const Icon(Icons.delete_outline, color: AppColors.danger),
                        ),
                      ),
                    ],
                  );
                }),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _ReferenceFormDialog extends ConsumerStatefulWidget {
  const _ReferenceFormDialog({required this.table, this.item});

  final RefTable table;
  final ReferenceItem? item;

  @override
  ConsumerState<_ReferenceFormDialog> createState() => _ReferenceFormDialogState();
}

class _ReferenceFormDialogState extends ConsumerState<_ReferenceFormDialog> with ServerFormState {
  late final Map<String, TextEditingController> _text = {
    for (final f in widget.table.fields)
      if (f.kind != FieldKind.brand)
        f.key: TextEditingController(text: '${widget.item?[f.key] ?? (f.kind == FieldKind.number ? 0 : '')}'),
  };
  late int? _brandId = widget.item?['brandId'] as int?;
  late bool _active = widget.item?.isActive ?? true;

  @override
  void dispose() {
    for (final c in _text.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final body = <String, dynamic>{
      for (final f in widget.table.fields)
        f.key: switch (f.kind) {
          FieldKind.brand => _brandId,
          FieldKind.number => int.tryParse(_text[f.key]!.text.trim()) ?? 0,
          FieldKind.text =>
            _text[f.key]!.text.trim().isEmpty && f.key == 'description' ? null : _text[f.key]!.text.trim(),
        },
      'isActive': _active,
    };
    final repository = ref.read(referenceRepositoryProvider);
    final item = widget.item;

    final saved = await submit(
      () => item == null
          ? repository.create(widget.table.path, body)
          : repository.update(widget.table.path, item.id, body),
    );
    if (saved && mounted) {
      showMessage(context, item == null ? 'Zapis „${body['name']}" je dodan.' : 'Izmjene su sačuvane.');
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final brands = ref.watch(lookupProvider).value?.brands ?? const [];

    return ActionDialog(
      title: widget.item == null ? 'Dodaj ${widget.table.singular}' : 'Uredi ${widget.table.singular}',
      confirmLabel: 'Sačuvaj',
      busy: busy,
      error: formError,
      onConfirm: _save,
      content: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final f in widget.table.fields)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: f.kind == FieldKind.brand
                    ? DropdownButtonFormField<int>(
                        initialValue: brands.any((b) => b.id == _brandId) ? _brandId : null,
                        isExpanded: true,
                        decoration: InputDecoration(labelText: f.label),
                        items: [for (final b in brands) DropdownMenuItem(value: b.id, child: Text(b.name))],
                        onChanged: (v) {
                          clearServerError(f.key);
                          setState(() => _brandId = v);
                        },
                        validator: (v) => v == null ? 'Izaberite marku.' : null,
                        forceErrorText: serverError(f.key),
                      )
                    : TextFormField(
                        controller: _text[f.key],
                        keyboardType: f.kind == FieldKind.number ? TextInputType.number : null,
                        decoration: InputDecoration(labelText: f.label, helperText: f.helper),
                        validator: f.validator,
                        forceErrorText: serverError(f.key),
                        onChanged: (_) => clearServerError(f.key),
                      ),
              ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Aktivno'),
              subtitle: const Text('Neaktivni zapisi se ne nude u novim unosima, ali ostaju na postojećim.'),
              value: _active,
              onChanged: (v) => setState(() => _active = v),
            ),
          ],
        ),
      ),
    );
  }
}
