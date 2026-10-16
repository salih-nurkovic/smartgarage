import '../../core/formatting.dart';
import 'common.dart';

class Review {
  const Review({
    required this.id,
    required this.serviceName,
    required this.authorDisplayName,
    required this.rating,
    required this.comment,
    required this.createdAtUtc,
    required this.isVisible,
    required this.isMine,
  });

  factory Review.fromJson(Json json) => Review(
        id: json['id'] as int,
        serviceName: json['serviceName'] as String,
        authorDisplayName: json['authorDisplayName'] as String,
        rating: json['rating'] as int,
        comment: json['comment'] as String?,
        createdAtUtc: parseUtc(json['createdAtUtc'] as String),
        isVisible: json['isVisible'] as bool,
        isMine: json['isMine'] as bool,
      );

  final int id;
  final String serviceName;
  final String authorDisplayName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;
  final bool isVisible;
  final bool isMine;
}

class ReviewSummary {
  const ReviewSummary({required this.averageRating, required this.totalCount, required this.distribution});

  factory ReviewSummary.fromJson(dynamic json) {
    final map = json as Json;
    final raw = map['distribution'] as Json? ?? const {};
    return ReviewSummary(
      averageRating: toDoubleOrNull(map['averageRating']),
      totalCount: map['totalCount'] as int,
      distribution: {for (final entry in raw.entries) int.parse(entry.key): entry.value as int},
    );
  }

  final double? averageRating;
  final int totalCount;

  /// Number of reviews per star rating, 1 to 5.
  final Map<int, int> distribution;

  int countFor(int stars) => distribution[stars] ?? 0;
}

/// A completed appointment the customer has not reviewed yet, with the services on it.
class ReviewableAppointment {
  const ReviewableAppointment({required this.appointmentId, required this.number, required this.completedAtUtc, required this.services});

  factory ReviewableAppointment.fromJson(Json json) => ReviewableAppointment(
        appointmentId: json['appointmentId'] as int,
        number: json['appointmentNumber'] as String,
        completedAtUtc: parseUtc(json['completedAtUtc'] as String),
        services: toList(json['services'], NamedItem.fromJson),
      );

  final int appointmentId;
  final String number;
  final DateTime completedAtUtc;
  final List<NamedItem> services;
}
