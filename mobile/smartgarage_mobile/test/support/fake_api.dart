import 'dart:typed_data';

import 'package:smartgarage_mobile/core/api/api_client.dart';
import 'package:smartgarage_mobile/core/api/api_exception.dart';
import 'package:smartgarage_mobile/core/api/token_storage.dart';
import 'package:smartgarage_mobile/core/providers.dart';
import 'package:smartgarage_mobile/data/models/user.dart';

/// Canned API answers in the exact shape the server sends, for rendering every screen without
/// a running backend.

final fakeUser = {
  'id': 5,
  'userName': 'amar',
  'email': 'amar@smartgarage.ba',
  'firstName': 'Amar',
  'lastName': 'Begić',
  'phoneNumber': '+387 62 100 200',
  'hasProfileImage': false,
  'isActive': true,
  'roles': ['Customer'],
  'createdAtUtc': '2025-03-10T10:00:00',
};

Map<String, dynamic> _paged(List<Map<String, dynamic>> items) =>
    {'items': items, 'page': 1, 'pageSize': 20, 'totalCount': items.length};

Map<String, dynamic> _appointment(int id, String status, String start, List<String> services, num price,
        {String payment = 'Unpaid'}) =>
    {
      'id': id,
      'appointmentNumber': 'APP-2026-000${500 + id}',
      'scheduledStartUtc': start,
      'scheduledEndUtc': start.replaceFirst('T08', 'T09').replaceFirst('T10', 'T11').replaceFirst('T14', 'T15'),
      'status': status,
      'paymentStatus': payment,
      'totalPrice': price,
      'vehicleId': 1,
      'vehicleDisplay': 'Volkswagen Golf 2018',
      'customerFullName': 'Amar Begić',
      'assignedEmployeeName': status == 'Pending' ? null : 'Adnan Suljagić',
      'serviceNames': services,
    };

final _services = [
  for (final (id, name, category, price, minutes, rating) in [
    (1, 'Mali servis', 'Redovno održavanje', 75, 60, 4.8),
    (2, 'Pregled kočnica', 'Kočnice', 40, 45, 4.9),
    (3, 'Zamjena zupčastog kaiša', 'Motor', 320, 240, 4.6),
    (4, 'Čišćenje DPF filtera', 'Motor', 150, 120, null),
  ])
    {
      'id': id,
      'name': name,
      'description': 'Kompletan servis: $name, uz provjeru svih potrebnih dijelova i tečnosti.',
      'categoryId': id,
      'categoryName': category,
      'price': price,
      'estimatedDurationMinutes': minutes,
      'imageUrl': null,
      'recommendedMileageIntervalKm': 15000 * id,
      'recommendedMonthsInterval': 12,
      'minVehicleAgeYears': null,
      'maxVehicleAgeYears': null,
      'isActive': true,
      'averageRating': rating,
      'reviewCount': rating == null ? 0 : 12,
      'bookingCount': 40,
      'tags': <Object>[],
      'engineTypes': id == 4
          ? [
              {'id': 1, 'name': 'Dizel'},
            ]
          : <Object>[],
    },
];

Object? _respond(String method, String path, Map<String, dynamic>? query) {
  final appointments = [
    _appointment(1, 'Confirmed', '2026-10-20T10:00:00', ['Mali servis'], 75),
    _appointment(2, 'Pending', '2026-10-27T08:00:00', ['Pregled kočnica', 'Mali servis'], 115),
    _appointment(3, 'Completed', '2026-09-14T08:00:00', ['Zamjena zupčastog kaiša'], 320),
    _appointment(4, 'Completed', '2026-08-02T14:00:00', ['Pregled kočnica'], 40, payment: 'Paid'),
    _appointment(5, 'Cancelled', '2026-07-11T10:00:00', ['Čišćenje DPF filtera'], 150),
  ];

  return switch (path) {
    '/api/lookup' => {
        'garage': {
          'name': 'Smart Garage Center',
          'address': 'Zmaja od Bosne 12, Sarajevo',
          'phone': '+387 33 123 456',
          'currency': 'EUR',
          'bookingMaxDaysAhead': 90,
        },
        'brands': [
          {
            'id': 1,
            'name': 'Volkswagen',
            'models': [
              {'id': 1, 'name': 'Golf'},
            ],
          },
        ],
        'engineTypes': [
          {'id': 1, 'name': 'Dizel'},
          {'id': 2, 'name': 'Benzin'},
        ],
        'serviceCategories': [
          {'id': 1, 'name': 'Redovno održavanje'},
        ],
        'serviceTags': <Object>[],
      },
    '/api/notifications/unread-count' => {'count': 2},
    '/api/appointments/summary' => {'total': 12, 'completed': 9, 'active': 2},
    '/api/appointments' => _paged(
        query?['status'] == 'Completed' ? appointments.where((a) => a['status'] == 'Completed').toList() : appointments,
      ),
    final p when p.startsWith('/api/appointments/') => {
        ..._appointment(3, 'Completed', '2026-09-14T08:00:00', ['Zamjena zupčastog kaiša', 'Pregled kočnica'], 360),
        'scheduledEndUtc': '2026-09-14T12:45:00',
        'customerNotes': 'Čuje se zvuk pri kočenju.',
        'internalNotes': null,
        'mileageAtAppointmentKm': 180000,
        'cancellationReason': null,
        'assignedEmployeePhone': '+387 61 222 333',
        'items': [
          {'serviceId': 3, 'serviceName': 'Zamjena zupčastog kaiša', 'unitPrice': 320, 'durationMinutes': 240, 'quantity': 1, 'lineTotal': 320},
          {'serviceId': 2, 'serviceName': 'Pregled kočnica', 'unitPrice': 40, 'durationMinutes': 45, 'quantity': 1, 'lineTotal': 40},
        ],
        'history': [
          {'fromStatus': null, 'toStatus': 'Pending', 'changedAtUtc': '2026-09-01T09:12:00', 'changedByName': 'Amar Begić', 'actorRole': 'Customer'},
          {'fromStatus': 'Pending', 'toStatus': 'Confirmed', 'changedAtUtc': '2026-09-02T08:30:00', 'changedByName': 'Admin Admin', 'actorRole': 'Administrator'},
          {'fromStatus': 'InProgress', 'toStatus': 'Completed', 'changedAtUtc': '2026-09-14T12:40:00', 'changedByName': 'Adnan Suljagić', 'actorRole': 'Employee'},
        ],
        'hasReview': false,
        'allowedActions': ['LeaveReview'],
        'changeBlockedReason': 'Završena rezervacija se više ne može mijenjati.',
        'canBePaid': true,
        'paymentBlockedReason': null,
      },
    '/api/vehicles' => _paged([
        {
          'id': 1,
          'ownerFullName': 'Amar Begić',
          'brandId': 1,
          'brandName': 'Volkswagen',
          'modelId': 1,
          'modelName': 'Golf',
          'engineTypeId': 1,
          'engineTypeName': 'Dizel',
          'productionYear': 2018,
          'registrationPlate': 'A12-K-345',
          'vin': 'WVWZZZ1KZAW123456',
          'currentMileageKm': 182400,
          'lastMileageUpdatedAtUtc': '2026-09-14T12:40:00',
          'lastServiceAtUtc': '2026-09-14T12:40:00',
          'nextServiceDueAtUtc': '2026-11-02T00:00:00',
          'appointmentCount': 55,
        },
        {
          'id': 2,
          'ownerFullName': 'Amar Begić',
          'brandId': 1,
          'brandName': 'BMW',
          'modelId': 1,
          'modelName': 'X3',
          'engineTypeId': 2,
          'engineTypeName': 'Benzin',
          'productionYear': 2022,
          'registrationPlate': 'H44-E-678',
          'vin': 'WBAXG71050DT23464',
          'currentMileageKm': 28700,
          'lastMileageUpdatedAtUtc': '2026-09-14T12:40:00',
          'lastServiceAtUtc': null,
          'nextServiceDueAtUtc': null,
          'appointmentCount': 0,
        },
      ]),
    '/api/services' => _paged(_services),
    final p when p.startsWith('/api/services/') => _services[2],
    '/api/reviews/summary' => {
        'averageRating': 4.7,
        'totalCount': 3,
        'distribution': {'5': 2, '4': 1},
      },
    '/api/reviews/reviewable' => [
        {
          'appointmentId': 3,
          'appointmentNumber': 'APP-2026-000503',
          'completedAtUtc': '2026-09-14T12:40:00',
          'services': [
            {'id': 3, 'name': 'Zamjena zupčastog kaiša'},
          ],
        },
      ],
    '/api/reviews' => _paged([
        {
          'id': 1,
          'serviceId': 1,
          'serviceName': 'Mali servis',
          'authorDisplayName': 'Haris M.',
          'rating': 5,
          'comment': 'Odličan servis, brza zamjena ulja i filtera. Osoblje veoma profesionalno.',
          'createdAtUtc': '2026-09-20T10:00:00',
          'isVisible': true,
          'isMine': false,
        },
        {
          'id': 2,
          'serviceId': 2,
          'serviceName': 'Pregled kočnica',
          'authorDisplayName': 'Amar B.',
          'rating': 4,
          'comment': 'Dobro urađeno, malo duže čekanje.',
          'createdAtUtc': '2026-09-18T10:00:00',
          'isVisible': true,
          'isMine': true,
        },
      ]),
    '/api/notifications' => _paged([
        {
          'id': 1,
          'type': 'AppointmentConfirmed',
          'title': 'Rezervacija potvrđena',
          'body': 'Vaša rezervacija APP-2026-000501 je potvrđena za 20.10.2026. u 10:00.',
          'isRead': false,
          'readAtUtc': null,
          'createdAtUtc': '2026-10-14T09:30:00',
          'relatedEntityType': 'Appointment',
          'relatedEntityId': 1,
        },
        {
          'id': 2,
          'type': 'ServiceReminder',
          'title': 'Podsjetnik za servis',
          'body': 'Na osnovu kilometraže (182.400 km) preporučujemo zamjenu filtera zraka.',
          'isRead': false,
          'readAtUtc': null,
          'createdAtUtc': '2026-10-13T08:00:00',
          'relatedEntityType': 'Vehicle',
          'relatedEntityId': 1,
        },
        {
          'id': 3,
          'type': 'PaymentSucceeded',
          'title': 'Plaćanje uspješno',
          'body': 'Uspješno ste platili 40,00 € za Pregled kočnica.',
          'isRead': true,
          'readAtUtc': '2026-08-02T16:00:00',
          'createdAtUtc': '2026-08-02T15:12:00',
          'relatedEntityType': 'Appointment',
          'relatedEntityId': 4,
        },
      ]),
    '/api/recommendations' => {
        'vehicle': {
          'vehicleId': 1,
          'display': 'Volkswagen Golf 2018',
          'productionYear': 2018,
          'mileageKm': 182400,
          'engineTypeName': 'Dizel',
          'ageYears': 8,
          'completedServiceCount': 55,
        },
        'algorithm': 'Content-Based',
        'algorithmVersion': 'cbf-1.0',
        'items': [
          {
            'serviceId': 3,
            'serviceName': 'Zamjena zupčastog kaiša',
            'categoryName': 'Motor',
            'price': 320,
            'estimatedDurationMinutes': 240,
            'recommendedMileageIntervalKm': 90000,
            'recommendedMonthsInterval': 60,
            'urgency': 'Urgent',
            'rank': 1,
            'score': 1.4,
            'explanation': 'vozilo ima 182400 km (interval: 90000 km); često se radi na vozilima Volkswagen Golf',
            'factors': <Object>[],
          },
          {
            'serviceId': 1,
            'serviceName': 'Zamjena filtera goriva',
            'categoryName': 'Redovno održavanje',
            'price': 60,
            'estimatedDurationMinutes': 45,
            'recommendedMileageIntervalKm': 40000,
            'recommendedMonthsInterval': null,
            'urgency': 'Planned',
            'rank': 2,
            'score': 1.2,
            'explanation':
                'već ste koristili usluge iz kategorije "Redovno održavanje"; od zadnje usluge prešli ste 35732 km (interval: 40000 km)',
            'factors': <Object>[],
          },
        ],
      },
    '/api/payments' => _paged([
        {
          'id': 1,
          'paymentNumber': 'PAY-2026-000469',
          'appointmentId': 4,
          'appointmentNumber': 'APP-2026-000504',
          'payerFullName': 'Amar Begić',
          'serviceNames': ['Pregled kočnica'],
          'amount': 115,
          'currency': 'EUR',
          'status': 'Succeeded',
          'createdAtUtc': '2026-08-02T15:10:00',
          'paidAtUtc': '2026-08-02T15:12:00',
          'failureReason': null,
          'amountCharged': 115,
          'refundedAmount': 57.5,
          'refundableAmount': 57.5,
          'refunds': [
            {
              'amount': 57.5,
              'reason': 'Djelimičan povrat zbog kašnjenja',
              'status': 'Succeeded',
              'requestedAtUtc': '2026-08-05T10:00:00',
              'completedAtUtc': '2026-08-05T10:00:05',
            },
          ],
        },
      ]),
    '/api/availability/slots' => [
        for (var hour = 8; hour <= 16; hour++)
          {
            'startUtc': '2026-10-20T${hour.toString().padLeft(2, '0')}:00:00Z',
            'endUtc': '2026-10-20T${(hour + 1).toString().padLeft(2, '0')}:00:00Z',
            'isAvailable': hour != 11 && hour != 12,
            'freePlaces': hour == 11 || hour == 12 ? 0 : 5 - hour % 3,
            'unavailableReason': hour == 11 || hour == 12 ? 'Termin je popunjen.' : null,
          },
      ],
    _ => throw ApiException('Nema lažnog odgovora za $method $path'),
  };
}

class FakeApi extends ApiClient {
  FakeApi() : super(TokenStorage());

  @override
  Future<T> get<T>(String path, T Function(dynamic json) parse, {Map<String, dynamic>? query}) async =>
      parse(_respond('GET', path, query));

  @override
  Future<Uint8List> bytes(String path) async => throw ApiException('Slike nisu dio testa.');
}

class FakeSignedIn extends AuthController {
  @override
  Future<User?> build() async => User.fromJson(fakeUser);
}

class FakeSignedOut extends AuthController {
  @override
  Future<User?> build() async => null;
}
