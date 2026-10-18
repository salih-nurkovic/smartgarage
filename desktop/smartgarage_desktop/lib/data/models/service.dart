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
    required this.minVehicleAgeYears,
    required this.maxVehicleAgeYears,
    required this.isActive,
    required this.averageRating,
    required this.reviewCount,
    required this.bookingCount,
    required this.tags,
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
    minVehicleAgeYears: json['minVehicleAgeYears'] as int?,
    maxVehicleAgeYears: json['maxVehicleAgeYears'] as int?,
    isActive: json['isActive'] as bool,
    averageRating: toDoubleOrNull(json['averageRating']),
    reviewCount: json['reviewCount'] as int,
    bookingCount: json['bookingCount'] as int,
    tags: toList(json['tags'], NamedItem.fromJson),
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
  final int? minVehicleAgeYears;
  final int? maxVehicleAgeYears;
  final bool isActive;
  final double? averageRating;
  final int reviewCount;
  final int bookingCount;
  final List<NamedItem> tags;
  final List<NamedItem> engineTypes;
}

class ServiceRequest {
  const ServiceRequest({
    required this.name,
    required this.description,
    required this.categoryId,
    required this.price,
    required this.durationMinutes,
    required this.mileageIntervalKm,
    required this.monthsInterval,
    required this.minVehicleAgeYears,
    required this.maxVehicleAgeYears,
    required this.isActive,
    required this.tagIds,
    required this.engineTypeIds,
  });

  final String name;
  final String description;
  final int categoryId;
  final double price;
  final int durationMinutes;
  final int? mileageIntervalKm;
  final int? monthsInterval;
  final int? minVehicleAgeYears;
  final int? maxVehicleAgeYears;
  final bool isActive;
  final List<int> tagIds;
  final List<int> engineTypeIds;

  Json toJson() => {
    'name': name,
    'description': description,
    'categoryId': categoryId,
    'price': price,
    'estimatedDurationMinutes': durationMinutes,
    'recommendedMileageIntervalKm': mileageIntervalKm,
    'recommendedMonthsInterval': monthsInterval,
    'minVehicleAgeYears': minVehicleAgeYears,
    'maxVehicleAgeYears': maxVehicleAgeYears,
    'isActive': isActive,
    'tagIds': tagIds,
    'engineTypeIds': engineTypeIds,
  };
}
