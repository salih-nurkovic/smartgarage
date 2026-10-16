import 'common.dart';

class ServiceItem {
  const ServiceItem({
    required this.id,
    required this.name,
    required this.description,
    required this.categoryId,
    required this.categoryName,
    required this.price,
    required this.durationMinutes,
    required this.imageUrl,
    required this.mileageIntervalKm,
    required this.monthsInterval,
    required this.averageRating,
    required this.reviewCount,
    required this.engineTypes,
  });

  factory ServiceItem.fromJson(Json json) => ServiceItem(
        id: json['id'] as int,
        name: json['name'] as String,
        description: json['description'] as String,
        categoryId: json['categoryId'] as int,
        categoryName: json['categoryName'] as String,
        price: toDouble(json['price']),
        durationMinutes: json['estimatedDurationMinutes'] as int,
        imageUrl: json['imageUrl'] as String?,
        mileageIntervalKm: json['recommendedMileageIntervalKm'] as int?,
        monthsInterval: json['recommendedMonthsInterval'] as int?,
        averageRating: toDoubleOrNull(json['averageRating']),
        reviewCount: json['reviewCount'] as int,
        engineTypes: toList(json['engineTypes'], NamedItem.fromJson),
      );

  final int id;
  final String name;
  final String description;
  final int categoryId;
  final String categoryName;
  final double price;
  final int durationMinutes;
  final String? imageUrl;
  final int? mileageIntervalKm;
  final int? monthsInterval;
  final double? averageRating;
  final int reviewCount;
  final List<NamedItem> engineTypes;

  /// A service with no engine list suits every engine; one with a list suits only those.
  bool suitsEngine(int engineTypeId) => engineTypes.isEmpty || engineTypes.any((e) => e.id == engineTypeId);
}

enum ServiceSort {
  name('Naziv', null, false),
  priceAscending('Cijena: najniža prvo', 'price', false),
  priceDescending('Cijena: najviša prvo', 'price', true),
  durationAscending('Trajanje: najkraće prvo', 'duration', false);

  const ServiceSort(this.label, this.orderBy, this.descending);

  final String label;
  final String? orderBy;
  final bool descending;
}

class ServiceFilter {
  const ServiceFilter({this.search = '', this.categoryId, this.maxPrice, this.maxDurationMinutes, this.sort = ServiceSort.name});

  final String search;
  final int? categoryId;
  final double? maxPrice;
  final int? maxDurationMinutes;
  final ServiceSort sort;

  int get activeCount => [categoryId, maxPrice, maxDurationMinutes].where((v) => v != null).length +
      (sort == ServiceSort.name ? 0 : 1);

  ServiceFilter copyWith({String? search}) => ServiceFilter(
        search: search ?? this.search,
        categoryId: categoryId,
        maxPrice: maxPrice,
        maxDurationMinutes: maxDurationMinutes,
        sort: sort,
      );

  Map<String, dynamic> toQuery() => {
        'search': search.trim(),
        'categoryId': categoryId,
        'maxPrice': maxPrice,
        'maxDurationMinutes': maxDurationMinutes,
        'orderBy': sort.orderBy,
        'descending': sort.descending,
        'isActive': true,
      };

  @override
  bool operator ==(Object other) =>
      other is ServiceFilter &&
      other.search == search &&
      other.categoryId == categoryId &&
      other.maxPrice == maxPrice &&
      other.maxDurationMinutes == maxDurationMinutes &&
      other.sort == sort;

  @override
  int get hashCode => Object.hash(search, categoryId, maxPrice, maxDurationMinutes, sort);
}
