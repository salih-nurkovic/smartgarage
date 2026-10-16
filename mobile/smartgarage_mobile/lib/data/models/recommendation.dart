import 'common.dart';

class RecommendedVehicle {
  const RecommendedVehicle({
    required this.display,
    required this.productionYear,
    required this.mileageKm,
    required this.engineTypeName,
    required this.ageYears,
    required this.completedServiceCount,
  });

  factory RecommendedVehicle.fromJson(Json json) => RecommendedVehicle(
        display: json['display'] as String,
        productionYear: json['productionYear'] as int,
        mileageKm: json['mileageKm'] as int,
        engineTypeName: json['engineTypeName'] as String,
        ageYears: json['ageYears'] as int,
        completedServiceCount: json['completedServiceCount'] as int,
      );

  final String display;
  final int productionYear;
  final int mileageKm;
  final String engineTypeName;
  final int ageYears;
  final int completedServiceCount;
}

class RecommendationItem {
  const RecommendationItem({
    required this.serviceId,
    required this.serviceName,
    required this.categoryName,
    required this.price,
    required this.mileageIntervalKm,
    required this.monthsInterval,
    required this.isUrgent,
    required this.explanation,
  });

  factory RecommendationItem.fromJson(Json json) => RecommendationItem(
        serviceId: json['serviceId'] as int,
        serviceName: json['serviceName'] as String,
        categoryName: json['categoryName'] as String,
        price: toDouble(json['price']),
        mileageIntervalKm: json['recommendedMileageIntervalKm'] as int?,
        monthsInterval: json['recommendedMonthsInterval'] as int?,
        isUrgent: json['urgency'] == 'Urgent',
        explanation: json['explanation'] as String,
      );

  final int serviceId;
  final String serviceName;
  final String categoryName;
  final double price;
  final int? mileageIntervalKm;
  final int? monthsInterval;
  final bool isUrgent;
  final String explanation;

  /// The reasons, one per line, as the mockup shows them.
  List<String> get reasons => explanation.split('; ');
}

class Recommendations {
  const Recommendations({required this.vehicle, required this.algorithm, required this.items});

  factory Recommendations.fromJson(dynamic json) {
    final map = json as Json;
    return Recommendations(
      vehicle: RecommendedVehicle.fromJson(map['vehicle'] as Json),
      algorithm: map['algorithm'] as String,
      items: toList(map['items'], RecommendationItem.fromJson),
    );
  }

  final RecommendedVehicle vehicle;
  final String algorithm;
  final List<RecommendationItem> items;
}
