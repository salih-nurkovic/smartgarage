import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartgarage_mobile/core/api/api_exception.dart';
import 'package:smartgarage_mobile/core/formatting.dart';
import 'package:smartgarage_mobile/data/models/appointment.dart';
import 'package:smartgarage_mobile/data/models/recommendation.dart';
import 'package:smartgarage_mobile/data/models/review.dart';

void main() {
  group('server timestamps', () {
    test('a value without a zone is read as UTC, so the garage hour is shown as it is', () {
      final value = parseUtc('2026-10-20T08:00:00');
      expect(value.isUtc, isTrue);
      expect(value.hour, 8);
    });

    test('a value with a zone keeps it', () {
      expect(parseUtc('2026-10-20T08:00:00Z').hour, 8);
      expect(parseUtc('2026-10-20T10:00:00+02:00').toUtc().hour, 8);
    });
  });

  test('an appointment detail is read with its actions and the reasons they are blocked', () {
    final detail = AppointmentDetail.fromJson({
      'id': 7,
      'appointmentNumber': 'APP-2026-000123',
      'scheduledStartUtc': '2026-10-20T08:00:00',
      'scheduledEndUtc': '2026-10-20T09:30:00',
      'status': 'Completed',
      'paymentStatus': 'Unpaid',
      'totalPrice': 115.5,
      'vehicleId': 3,
      'vehicleDisplay': 'Audi A4 2018',
      'customerFullName': 'Amar Begić',
      'assignedEmployeeName': 'Adnan Suljagić',
      'serviceNames': ['Mali servis', 'Pregled kočnica'],
      'items': [
        {'serviceId': 1, 'serviceName': 'Mali servis', 'unitPrice': 75, 'durationMinutes': 60, 'quantity': 1, 'lineTotal': 75},
        {'serviceId': 5, 'serviceName': 'Pregled kočnica', 'unitPrice': 40.5, 'durationMinutes': 30, 'quantity': 1, 'lineTotal': 40.5},
      ],
      'history': [
        {'fromStatus': null, 'toStatus': 'Pending', 'changedAtUtc': '2026-10-10T09:00:00', 'changedByName': 'Amar Begić', 'actorRole': 'Customer'},
      ],
      'hasReview': false,
      'allowedActions': ['LeaveReview'],
      'changeBlockedReason': 'Završena rezervacija se ne može mijenjati.',
      'canBePaid': true,
      'paymentBlockedReason': null,
    });

    expect(detail.number, 'APP-2026-000123');
    expect(detail.status, AppointmentStatus.completed);
    expect(detail.paymentStatus.isPaid, isFalse);
    expect(detail.durationMinutes, 90);
    expect(detail.title, 'Mali servis i još 1');
    expect(detail.can(AppointmentActions.leaveReview), isTrue);
    expect(detail.can(AppointmentActions.cancel), isFalse);
    expect(detail.history.single.toStatus, AppointmentStatus.pending);
  });

  test('status filters are sent in the server enum name', () {
    expect(AppointmentStatus.inProgress.apiName, 'InProgress');
    expect(AppointmentStatus.parse('InProgress'), AppointmentStatus.inProgress);
  });

  test('a recommendation explanation splits into the reasons shown on the card', () {
    final item = RecommendationItem.fromJson({
      'serviceId': 4,
      'serviceName': 'Zamjena zupčastog kaiša',
      'categoryName': 'Motor',
      'price': 320,
      'recommendedMileageIntervalKm': 90000,
      'recommendedMonthsInterval': 60,
      'urgency': 'Urgent',
      'explanation': 'vozilo ima 182000 km (interval: 90000 km); često se radi na vozilima Audi A4',
    });

    expect(item.isUrgent, isTrue);
    expect(item.reasons, ['vozilo ima 182000 km (interval: 90000 km)', 'često se radi na vozilima Audi A4']);
  });

  test('the rating distribution is keyed by star count', () {
    final summary = ReviewSummary.fromJson({
      'averageRating': 4.7,
      'totalCount': 3,
      'distribution': {'5': 2, '4': 1},
    });

    expect(summary.countFor(5), 2);
    expect(summary.countFor(4), 1);
    expect(summary.countFor(1), 0);
  });

  group('API errors', () {
    DioException failure(int status, Object body) => DioException(
          requestOptions: RequestOptions(path: '/api/vehicles'),
          response: Response(requestOptions: RequestOptions(path: '/api/vehicles'), statusCode: status, data: body),
        );

    test('field errors from the server are kept for the matching input', () {
      final error = ApiException.fromDio(failure(400, {
        'status': 400,
        'title': 'Neispravni podaci',
        'detail': 'Jedno ili više polja nije ispravno.',
        'correlationId': 'abc',
        'errors': {
          'vin': ['Vozilo sa ovim brojem šasije već postoji.'],
        },
      }));

      expect(error.statusCode, 400);
      expect(error.fieldErrors['vin'], 'Vozilo sa ovim brojem šasije već postoji.');
    });

    test('the server message is passed through as it is', () {
      final error = ApiException.fromDio(failure(409, {
        'status': 409,
        'title': 'Sukob sa postojećim podacima',
        'detail': 'Vozilo ima rezervacije i ne može se obrisati.',
        'correlationId': 'abc',
      }));

      expect(error.message, 'Vozilo ima rezervacije i ne može se obrisati.');
    });

    test('no answer at all reads as a connection problem', () {
      final error = ApiException.fromDio(
        DioException(requestOptions: RequestOptions(path: '/'), type: DioExceptionType.connectionError),
      );
      expect(error.message, contains('Server nije dostupan'));
    });
  });
}
