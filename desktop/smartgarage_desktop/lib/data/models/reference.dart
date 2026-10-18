import 'common.dart';

/// A row of one of the reference tables (brands, models, engine types, categories, tags). The
/// tables differ only in a few fields, so one shape holds them all; [fields] carries the
/// table-specific values by their API names.
class ReferenceItem {
  const ReferenceItem({required this.id, required this.name, required this.isActive, required this.fields});

  factory ReferenceItem.fromJson(Json json) => ReferenceItem(
    id: json['id'] as int,
    name: json['name'] as String,
    isActive: json['isActive'] as bool,
    fields: json,
  );

  final int id;
  final String name;
  final bool isActive;
  final Json fields;

  Object? operator [](String field) => fields[field];
}
