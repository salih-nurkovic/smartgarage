import 'dart:typed_data';

import 'package:smartgarage_desktop/core/api/api_client.dart';
import 'package:smartgarage_desktop/core/api/api_exception.dart';
import 'package:smartgarage_desktop/core/api/token_storage.dart';
import 'package:smartgarage_desktop/core/providers.dart';
import 'package:smartgarage_desktop/data/models/user.dart';

/// Canned API answers in the exact shape the server sends, for rendering every screen without
/// a running backend.

Map<String, dynamic> _user(String role) => {
  'id': 1,
  'userName': role == 'Administrator' ? 'admin' : 'employee',
  'email': 'osoblje@smartgarage.ba',
  'firstName': role == 'Administrator' ? 'Salih' : 'Adnan',
  'lastName': role == 'Administrator' ? 'Nurković' : 'Suljagić',
  'phoneNumber': '+387 61 111 222',
  'hasProfileImage': false,
  'isActive': true,
  'roles': [role],
  'createdAtUtc': '2025-01-10T10:00:00',
  'lastLoginAtUtc': '2026-10-16T08:00:00',
};

Map<String, dynamic> _paged(List<Map<String, dynamic>> items) => {
  'items': items,
  'page': 1,
  'pageSize': 20,
  'totalCount': items.length,
};

Map<String, dynamic> _appointment(int id, String status, String start, String customer) => {
  'id': id,
  'appointmentNumber': 'APP-2026-000${600 + id}',
  'scheduledStartUtc': start,
  'scheduledEndUtc': start.replaceFirst('T10', 'T11').replaceFirst('T08', 'T09'),
  'status': status,
  'paymentStatus': status == 'Completed' ? 'Paid' : 'Unpaid',
  'totalPrice': 115.0,
  'vehicleId': 1,
  'vehicleDisplay': 'Volkswagen Golf (A12-K-345)',
  'customerFullName': customer,
  'assignedEmployeeName': status == 'Pending' ? null : 'Adnan Suljagić',
  'serviceNames': ['Mali servis', 'Pregled kočnica'],
};

final _appointments = [
  _appointment(1, 'Confirmed', '2026-10-20T10:00:00', 'Amar Begić'),
  _appointment(2, 'Pending', '2026-10-21T08:00:00', 'Lejla Suljagić'),
  _appointment(3, 'Completed', '2026-09-14T08:00:00', 'Nedim Suljić'),
  _appointment(4, 'Cancelled', '2026-09-02T10:00:00', 'Ivana Perić'),
];

Map<String, dynamic> _reference(int id, String name, Map<String, dynamic> extra) => {
  'id': id,
  'name': name,
  'isActive': true,
  ...extra,
};

Object? _respond(String path) => switch (path) {
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
      {'id': 2, 'name': 'Kočnice'},
    ],
    'serviceTags': [
      {'id': 1, 'name': 'Ulje'},
    ],
  },
  '/api/dashboard' => {
    'customers': {'value': 1247, 'changePercent': 12.0},
    'activeAppointments': {'value': 89, 'changePercent': 8.0},
    'completedServices': {'value': 456, 'changePercent': -3.5},
    'monthRevenue': {'value': 45890.0, 'changePercent': 15.0},
    'revenueByMonth': [
      for (var m = 5; m <= 10; m++) {'year': 2026, 'month': m, 'revenue': 30000.0 + m * 2000},
    ],
    'serviceDistribution': [
      {'name': 'Mali servis', 'count': 35, 'percent': 35.0},
      {'name': 'Pregled kočnica', 'count': 25, 'percent': 25.0},
      {'name': 'Zamjena guma', 'count': 20, 'percent': 20.0},
      {'name': 'Dijagnostika', 'count': 15, 'percent': 15.0},
      {'name': 'Ostalo', 'count': 5, 'percent': 5.0},
    ],
  },
  '/api/appointments/summary' => {
    'total': 89,
    'pending': 12,
    'confirmed': 45,
    'inProgress': 0,
    'completed': 32,
    'cancelled': 0,
    'active': 57,
  },
  '/api/appointments' => _paged(_appointments),
  final p when p.startsWith('/api/appointments/') => {
    ..._appointments[1],
    'customerNotes': 'Čuje se zvuk pri kočenju.',
    'internalNotes': null,
    'mileageAtAppointmentKm': null,
    'cancellationReason': null,
    'confirmedAtUtc': null,
    'completedAtUtc': null,
    'cancelledAtUtc': null,
    'assignedEmployeePhone': null,
    'items': [
      {
        'serviceId': 1,
        'serviceName': 'Mali servis',
        'unitPrice': 75,
        'durationMinutes': 60,
        'quantity': 1,
        'lineTotal': 75,
      },
      {
        'serviceId': 2,
        'serviceName': 'Pregled kočnica',
        'unitPrice': 40,
        'durationMinutes': 45,
        'quantity': 1,
        'lineTotal': 40,
      },
    ],
    'history': [
      {
        'fromStatus': null,
        'toStatus': 'Pending',
        'changedAtUtc': '2026-10-10T09:00:00',
        'changedByName': 'Lejla Suljagić',
        'actorRole': 'Customer',
        'reason': null,
      },
    ],
    'hasReview': false,
    'allowedActions': ['Confirm', 'Cancel', 'Reschedule'],
    'changeBlockedReason': null,
    'canBePaid': false,
    'paymentBlockedReason': 'Plaćanje je moguće nakon završenog servisa.',
  },
  '/api/services' => _paged([
    for (final (id, name, bookings) in [(1, 'Mali servis', 245), (2, 'Pregled kočnica', 0)])
      {
        'id': id,
        'name': name,
        'description': 'Kompletan servis: $name.',
        'categoryId': id,
        'categoryName': id == 1 ? 'Redovno održavanje' : 'Kočnice',
        'price': 75.0,
        'estimatedDurationMinutes': 60,
        'imageUrl': null,
        'recommendedMileageIntervalKm': 15000,
        'recommendedMonthsInterval': 12,
        'minVehicleAgeYears': null,
        'maxVehicleAgeYears': null,
        'isActive': true,
        'averageRating': 4.8,
        'reviewCount': 12,
        'bookingCount': bookings,
        'tags': [
          {'id': 1, 'name': 'Ulje'},
        ],
        'engineTypes': <Object>[],
      },
  ]),
  '/api/employees' => _paged([
    {
      'id': 1,
      'userId': 7,
      'userName': 'adnan',
      'firstName': 'Adnan',
      'lastName': 'Suljagić',
      'email': 'adnan.suljagic@smartgarage.ba',
      'phoneNumber': '+387 61 111 222',
      'position': 'Glavni mehaničar',
      'specialization': 'Motori',
      'hireDateUtc': '2011-03-01T00:00:00',
      'isActive': true,
      'isAvailableForAssignment': true,
      'completedJobs': 1245,
      'averageRating': 4.9,
      'schedule': [
        for (final day in ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'])
          {
            'dayOfWeek': day,
            'startTime': '08:00:00',
            'endTime': '17:00:00',
            'isWorkingDay': day != 'Saturday' && day != 'Sunday',
          },
      ],
    },
  ]),
  '/api/users' => _paged([
    _user('Administrator'),
    {..._user('Customer'), 'id': 2, 'userName': 'amar'},
  ]),
  '/api/payments/summary' => {'netRevenue': 290.0, 'succeeded': 3, 'inProgress': 1, 'failed': 1},
  '/api/payments' => _paged([
    {
      'id': 1,
      'paymentNumber': 'PAY-2026-000469',
      'appointmentId': 3,
      'appointmentNumber': 'APP-2026-000603',
      'payerFullName': 'Nedim Suljić',
      'serviceNames': ['Mali servis'],
      'amount': 115.0,
      'currency': 'EUR',
      'status': 'Succeeded',
      'createdAtUtc': '2026-09-14T13:00:00',
      'paidAtUtc': '2026-09-14T13:01:00',
      'failureReason': null,
      'amountCharged': 115.0,
      'refundedAmount': 57.5,
      'refundableAmount': 57.5,
      'refunds': [
        {
          'amount': 57.5,
          'reason': 'Djelimičan povrat',
          'status': 'Succeeded',
          'requestedAtUtc': '2026-09-15T10:00:00',
          'completedAtUtc': '2026-09-15T10:00:05',
        },
      ],
    },
  ]),
  '/api/reviews' => _paged([
    {
      'id': 1,
      'serviceId': 1,
      'serviceName': 'Mali servis',
      'authorDisplayName': 'Haris M.',
      'rating': 5,
      'comment': 'Odličan servis.',
      'createdAtUtc': '2026-09-20T10:00:00',
      'isVisible': true,
      'isMine': false,
    },
  ]),
  '/api/service-categories' => _paged([
    _reference(1, 'Redovno održavanje', {'description': null, 'displayOrder': 1, 'serviceCount': 8}),
  ]),
  '/api/service-tags' => _paged([
    _reference(1, 'Ulje', {'slug': 'oil', 'serviceCount': 3}),
  ]),
  '/api/vehicle-brands' => _paged([
    _reference(1, 'Volkswagen', {'modelCount': 4}),
  ]),
  '/api/vehicle-models' => _paged([
    _reference(1, 'Golf', {'brandId': 1, 'brandName': 'Volkswagen', 'vehicleCount': 0}),
  ]),
  '/api/engine-types' => _paged([
    _reference(1, 'Dizel', {'code': 'diesel', 'vehicleCount': 6}),
  ]),
  '/api/notifications/unread-count' => {'count': 1},
  '/api/notifications' => _paged([
    {
      'id': 1,
      'type': 'AppointmentCreated',
      'title': 'Nova rezervacija',
      'body': 'Lejla Suljagić je zakazala termin 21.10.2026. u 08:00.',
      'isRead': false,
      'readAtUtc': null,
      'createdAtUtc': '2026-10-15T09:30:00',
      'relatedEntityType': 'Appointment',
      'relatedEntityId': 2,
    },
  ]),
  '/api/error-logs' => _paged([
    {
      'id': 1,
      'correlationId': '0HNPBJ673AAM3:00000001',
      'source': 'API',
      'level': 'Error',
      'message': 'Connection Timeout Expired.',
      'exceptionType': 'Microsoft.Data.SqlClient.SqlException',
      'httpMethod': 'POST',
      'requestPath': '/api/auth/login',
      'statusCode': 500,
      'userName': null,
      'occurredAtUtc': '2026-10-16T18:34:32',
    },
  ]),
  _ => throw ApiException('Nema lažnog odgovora za $path'),
};

class FakeApi extends ApiClient {
  FakeApi() : super(TokenStorage());

  @override
  Future<T> get<T>(String path, T Function(dynamic json) parse, {Map<String, dynamic>? query}) async =>
      parse(_respond(path));

  @override
  Future<Uint8List> bytes(String path) async => throw ApiException('Slike nisu dio testa.');
}

class FakeAdministrator extends AuthController {
  @override
  Future<User?> build() async => User.fromJson(_user('Administrator'));
}

class FakeEmployee extends AuthController {
  @override
  Future<User?> build() async => User.fromJson(_user('Employee'));
}
