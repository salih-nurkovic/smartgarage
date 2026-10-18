import 'common.dart';

class LookupBrand {
  const LookupBrand(this.id, this.name, this.models);

  factory LookupBrand.fromJson(Json json) =>
      LookupBrand(json['id'] as int, json['name'] as String, toList(json['models'], NamedItem.fromJson));

  final int id;
  final String name;
  final List<NamedItem> models;
}

class GarageInfo {
  const GarageInfo({
    required this.name,
    required this.address,
    required this.phone,
    required this.currency,
    required this.bookingMaxDaysAhead,
  });

  factory GarageInfo.fromJson(Json json) => GarageInfo(
    name: json['name'] as String,
    address: json['address'] as String,
    phone: json['phone'] as String,
    currency: json['currency'] as String,
    bookingMaxDaysAhead: json['bookingMaxDaysAhead'] as int,
  );

  final String name;
  final String address;
  final String phone;
  final String currency;
  final int bookingMaxDaysAhead;
}

/// Everything the dropdowns need, from one cached endpoint.
class Lookup {
  const Lookup({
    required this.garage,
    required this.brands,
    required this.engineTypes,
    required this.categories,
    required this.tags,
  });

  factory Lookup.fromJson(dynamic json) {
    final map = json as Json;
    return Lookup(
      garage: GarageInfo.fromJson(map['garage'] as Json),
      brands: toList(map['brands'], LookupBrand.fromJson),
      engineTypes: toList(map['engineTypes'], NamedItem.fromJson),
      categories: toList(map['serviceCategories'], NamedItem.fromJson),
      tags: toList(map['serviceTags'], NamedItem.fromJson),
    );
  }

  final GarageInfo garage;
  final List<LookupBrand> brands;
  final List<NamedItem> engineTypes;
  final List<NamedItem> categories;
  final List<NamedItem> tags;
}
