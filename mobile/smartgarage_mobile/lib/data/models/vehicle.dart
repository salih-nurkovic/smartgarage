import '../../core/formatting.dart';
import 'common.dart';

class Vehicle {
  const Vehicle({
    required this.id,
    required this.brandId,
    required this.brandName,
    required this.modelId,
    required this.modelName,
    required this.engineTypeId,
    required this.engineTypeName,
    required this.productionYear,
    required this.registrationPlate,
    required this.vin,
    required this.currentMileageKm,
    required this.lastServiceAtUtc,
    required this.nextServiceDueAtUtc,
    required this.appointmentCount,
  });

  factory Vehicle.fromJson(Json json) => Vehicle(
        id: json['id'] as int,
        brandId: json['brandId'] as int,
        brandName: json['brandName'] as String,
        modelId: json['modelId'] as int,
        modelName: json['modelName'] as String,
        engineTypeId: json['engineTypeId'] as int,
        engineTypeName: json['engineTypeName'] as String,
        productionYear: json['productionYear'] as int,
        registrationPlate: json['registrationPlate'] as String,
        vin: json['vin'] as String,
        currentMileageKm: json['currentMileageKm'] as int,
        lastServiceAtUtc: parseUtcOrNull(json['lastServiceAtUtc']),
        nextServiceDueAtUtc: parseUtcOrNull(json['nextServiceDueAtUtc']),
        appointmentCount: json['appointmentCount'] as int,
      );

  final int id;
  final int brandId;
  final String brandName;
  final int modelId;
  final String modelName;
  final int engineTypeId;
  final String engineTypeName;
  final int productionYear;
  final String registrationPlate;
  final String vin;
  final int currentMileageKm;
  final DateTime? lastServiceAtUtc;
  final DateTime? nextServiceDueAtUtc;
  final int appointmentCount;

  String get displayName => '$brandName $modelName';
}

class VehicleRequest {
  const VehicleRequest({
    required this.brandId,
    required this.modelId,
    required this.engineTypeId,
    required this.productionYear,
    required this.registrationPlate,
    required this.vin,
    required this.currentMileageKm,
  });

  final int brandId;
  final int modelId;
  final int engineTypeId;
  final int productionYear;
  final String registrationPlate;
  final String vin;
  final int currentMileageKm;

  Json toJson() => {
        'brandId': brandId,
        'modelId': modelId,
        'engineTypeId': engineTypeId,
        'productionYear': productionYear,
        'registrationPlate': registrationPlate,
        'vin': vin,
        'currentMileageKm': currentMileageKm,
      };
}
