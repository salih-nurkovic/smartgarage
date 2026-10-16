typedef Json = Map<String, dynamic>;

/// One page of a list endpoint. Every list in the API is paged.
class Paged<T> {
  Paged({required this.items, required this.page, required this.pageSize, required this.totalCount});

  factory Paged.fromJson(dynamic json, T Function(Json) item) {
    final map = json as Json;
    return Paged(
      items: (map['items'] as List).map((e) => item(e as Json)).toList(),
      page: map['page'] as int,
      pageSize: map['pageSize'] as int,
      totalCount: map['totalCount'] as int,
    );
  }

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;
}

class NamedItem {
  const NamedItem(this.id, this.name);

  factory NamedItem.fromJson(Json json) => NamedItem(json['id'] as int, json['name'] as String);

  final int id;
  final String name;
}

double toDouble(Object? value) => (value as num).toDouble();

double? toDoubleOrNull(Object? value) => (value as num?)?.toDouble();

List<String> toStrings(Object? value) => (value as List? ?? const []).cast<String>();

List<T> toList<T>(Object? value, T Function(Json) item) =>
    (value as List? ?? const []).map((e) => item(e as Json)).toList();
