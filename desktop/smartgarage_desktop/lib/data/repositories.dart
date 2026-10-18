import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../core/api/api_client.dart';
import 'models/admin.dart';
import 'models/appointment.dart';
import 'models/common.dart';
import 'models/employee.dart';
import 'models/lookup.dart';
import 'models/notification.dart';
import 'models/reference.dart';
import 'models/service.dart';
import 'models/user.dart';

const pageSize = 20;

class AuthRepository {
  const AuthRepository(this._api);

  final ApiClient _api;

  Future<AuthResult> login(String userName, String password) =>
      _api.anonymous('/api/auth/login', AuthResult.fromJson, body: {'userName': userName, 'password': password});

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) => _api.post(
    '/api/auth/change-password',
    (_) {},
    body: {'currentPassword': currentPassword, 'newPassword': newPassword, 'confirmNewPassword': confirmNewPassword},
  );

  /// Ends the session on the server, not only in the application.
  Future<void> logout() => _api.post('/api/auth/logout', (_) {});
}

class ProfileRepository {
  const ProfileRepository(this._api);

  final ApiClient _api;

  Future<User> get() => _api.get('/api/profile', (json) => User.fromJson(json as Json));

  Future<User> update({
    required String email,
    required String firstName,
    required String lastName,
    required String phoneNumber,
  }) => _api.put(
    '/api/profile',
    (json) => User.fromJson(json as Json),
    body: {'email': email, 'firstName': firstName, 'lastName': lastName, 'phoneNumber': phoneNumber},
  );

  Future<Uint8List> image() => _api.bytes('/api/profile/image');

  Future<User> uploadImage(Uint8List bytes, String fileName) => _api.post(
    '/api/profile/image',
    (json) => User.fromJson(json as Json),
    body: FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: fileName)}),
  );
}

class DashboardRepository {
  const DashboardRepository(this._api);

  final ApiClient _api;

  Future<Dashboard> get() => _api.get('/api/dashboard', Dashboard.fromJson);

  Future<Lookup> lookup() => _api.get('/api/lookup', Lookup.fromJson);

  Future<Uint8List> image(String url) => _api.bytes(url);
}

class AppointmentQuery {
  const AppointmentQuery({this.search = '', this.status, this.from, this.to, this.employeeId});

  final String search;
  final AppointmentStatus? status;
  final DateTime? from;
  final DateTime? to;
  final int? employeeId;

  Map<String, dynamic> toQuery() => {
    'search': search.trim(),
    'status': status == null ? null : apiNameOf(status!),
    'from': from?.toIso8601String(),
    'to': to?.toIso8601String(),
    'employeeId': employeeId,
  };

  @override
  bool operator ==(Object other) =>
      other is AppointmentQuery &&
      other.search == search &&
      other.status == status &&
      other.from == from &&
      other.to == to &&
      other.employeeId == employeeId;

  @override
  int get hashCode => Object.hash(search, status, from, to, employeeId);
}

class AppointmentRepository {
  const AppointmentRepository(this._api);

  final ApiClient _api;

  Future<Paged<AppointmentListItem>> list(AppointmentQuery query, {int page = 1}) => _api.get(
    '/api/appointments',
    (json) => Paged.fromJson(json, AppointmentListItem.fromJson),
    query: {...query.toQuery(), 'page': page, 'pageSize': pageSize},
  );

  /// The next appointments from now on, soonest first.
  Future<List<AppointmentListItem>> upcoming(DateTime fromUtc, {int count = 6}) => _api.get(
    '/api/appointments',
    (json) => Paged.fromJson(json, AppointmentListItem.fromJson).items,
    query: {'from': fromUtc.toIso8601String(), 'orderBy': 'start', 'pageSize': count},
  );

  Future<AppointmentSummary> summary() => _api.get('/api/appointments/summary', AppointmentSummary.fromJson);

  Future<AppointmentDetail> detail(int id) => _api.get('/api/appointments/$id', AppointmentDetail.fromJson);

  Future<AppointmentDetail> confirm(int id) => _api.post('/api/appointments/$id/confirm', AppointmentDetail.fromJson);

  Future<AppointmentDetail> start(int id) => _api.post('/api/appointments/$id/start', AppointmentDetail.fromJson);

  Future<AppointmentDetail> complete(int id, {required int mileageKm, String? internalNotes}) => _api.post(
    '/api/appointments/$id/complete',
    AppointmentDetail.fromJson,
    body: {'mileageAtAppointmentKm': mileageKm, 'internalNotes': internalNotes},
  );

  Future<AppointmentDetail> cancel(int id, String reason) =>
      _api.post('/api/appointments/$id/cancel', AppointmentDetail.fromJson, body: {'reason': reason});

  Future<AppointmentDetail> reschedule(int id, DateTime newStartUtc, String? reason) => _api.put(
    '/api/appointments/$id/reschedule',
    AppointmentDetail.fromJson,
    body: {'newStartUtc': newStartUtc.toIso8601String(), 'reason': reason},
  );

  Future<AppointmentDetail> assign(int id, int employeeId) =>
      _api.post('/api/appointments/$id/assign', AppointmentDetail.fromJson, body: {'employeeId': employeeId});

  Future<List<Slot>> slots(DateTime day, List<int> serviceIds) => _api.get(
    '/api/availability/slots',
    (json) => toList(json, Slot.fromJson),
    query: {'date': '${day.year}-${_two(day.month)}-${_two(day.day)}', 'serviceIds': serviceIds},
  );

  static String _two(int value) => value.toString().padLeft(2, '0');
}

class ServiceRepository {
  const ServiceRepository(this._api);

  final ApiClient _api;

  Future<Paged<ServiceItem>> list({String search = '', int? categoryId, bool? isActive, int page = 1}) => _api.get(
    '/api/services',
    (json) => Paged.fromJson(json, ServiceItem.fromJson),
    query: {
      'search': search.trim(),
      'categoryId': categoryId,
      'isActive': isActive,
      'page': page,
      'pageSize': pageSize,
    },
  );

  Future<ServiceItem> create(ServiceRequest request) =>
      _api.post('/api/services', (json) => ServiceItem.fromJson(json as Json), body: request.toJson());

  Future<ServiceItem> update(int id, ServiceRequest request) =>
      _api.put('/api/services/$id', (json) => ServiceItem.fromJson(json as Json), body: request.toJson());

  Future<void> delete(int id) => _api.delete('/api/services/$id');

  Future<ServiceItem> uploadImage(int id, Uint8List bytes, String fileName) => _api.post(
    '/api/services/$id/image',
    (json) => ServiceItem.fromJson(json as Json),
    body: FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: fileName)}),
  );
}

class EmployeeRepository {
  const EmployeeRepository(this._api);

  final ApiClient _api;

  Future<Paged<Employee>> list({String search = '', bool? isActive, int page = 1, int size = pageSize}) => _api.get(
    '/api/employees',
    (json) => Paged.fromJson(json, Employee.fromJson),
    query: {'search': search.trim(), 'isActive': isActive, 'page': page, 'pageSize': size},
  );

  Future<Employee> create(Json body) =>
      _api.post('/api/employees', (json) => Employee.fromJson(json as Json), body: body);

  Future<Employee> update(int id, Json body) =>
      _api.put('/api/employees/$id', (json) => Employee.fromJson(json as Json), body: body);

  Future<Employee> saveSchedule(int id, List<ScheduleDay> days) => _api.put(
    '/api/employees/$id/schedule',
    (json) => Employee.fromJson(json as Json),
    body: {
      'days': [for (final day in days) day.toJson()],
    },
  );

  Future<void> delete(int id) => _api.delete('/api/employees/$id');
}

class UserRepository {
  const UserRepository(this._api);

  final ApiClient _api;

  Future<Paged<ManagedUser>> list({String search = '', String? role, bool? isActive, int page = 1}) => _api.get(
    '/api/users',
    (json) => Paged.fromJson(json, ManagedUser.fromJson),
    query: {'search': search.trim(), 'role': role, 'isActive': isActive, 'page': page, 'pageSize': pageSize},
  );

  Future<ManagedUser> create(Json body) =>
      _api.post('/api/users', (json) => ManagedUser.fromJson(json as Json), body: body);

  Future<ManagedUser> update(int id, Json body) =>
      _api.put('/api/users/$id', (json) => ManagedUser.fromJson(json as Json), body: body);

  Future<ManagedUser> setStatus(int id, {required bool isActive, String? reason}) => _api.patch(
    '/api/users/$id/status',
    (json) => ManagedUser.fromJson(json as Json),
    body: {'isActive': isActive, 'reason': reason},
  );
}

class PaymentRepository {
  const PaymentRepository(this._api);

  final ApiClient _api;

  Future<Paged<Payment>> list({String search = '', PaymentStatus? status, int page = 1}) => _api.get(
    '/api/payments',
    (json) => Paged.fromJson(json, Payment.fromJson),
    query: {
      'search': search.trim(),
      'status': status == null ? null : apiNameOf(status),
      'page': page,
      'pageSize': pageSize,
    },
  );

  Future<PaymentSummary> summary() => _api.get('/api/payments/summary', PaymentSummary.fromJson);

  Future<Payment> get(int id) => _api.get('/api/payments/$id', Payment.fromJson);

  /// The server caps the amount at what Stripe actually charged, less earlier refunds.
  Future<Payment> refund(int id, {double? amount, required String reason}) =>
      _api.post('/api/payments/$id/refund', Payment.fromJson, body: {'amount': amount, 'reason': reason});
}

class ReviewRepository {
  const ReviewRepository(this._api);

  final ApiClient _api;

  Future<Paged<Review>> list({int? rating, String search = '', int page = 1}) => _api.get(
    '/api/reviews',
    (json) => Paged.fromJson(json, Review.fromJson),
    query: {'rating': rating, 'search': search.trim(), 'page': page, 'pageSize': pageSize},
  );

  Future<void> setVisible(int id, {required bool visible}) =>
      _api.patch('/api/reviews/$id/visibility', (_) {}, body: {'isVisible': visible});
}

/// One generic client for the five reference tables; they share the same endpoints.
class ReferenceRepository {
  const ReferenceRepository(this._api);

  final ApiClient _api;

  Future<Paged<ReferenceItem>> list(String path, {String search = '', Map<String, dynamic>? filters, int page = 1}) =>
      _api.get(
        path,
        (json) => Paged.fromJson(json, ReferenceItem.fromJson),
        query: {'search': search.trim(), ...?filters, 'page': page, 'pageSize': pageSize},
      );

  Future<void> create(String path, Json body) => _api.post(path, (_) {}, body: body);

  Future<void> update(String path, int id, Json body) => _api.put('$path/$id', (_) {}, body: body);

  Future<void> delete(String path, int id) => _api.delete('$path/$id');
}

class NotificationRepository {
  const NotificationRepository(this._api);

  final ApiClient _api;

  Future<Paged<AppNotification>> list({int page = 1}) => _api.get(
    '/api/notifications',
    (json) => Paged.fromJson(json, AppNotification.fromJson),
    query: {'page': page, 'pageSize': pageSize},
  );

  Future<int> unreadCount() => _api.get('/api/notifications/unread-count', (json) => (json as Json)['count'] as int);

  Future<void> markRead(int id) => _api.patch('/api/notifications/$id/read', (_) {});

  Future<void> markAllRead() => _api.patch('/api/notifications/read-all', (_) {});

  Future<void> delete(int id) => _api.delete('/api/notifications/$id');
}

class ErrorLogRepository {
  const ErrorLogRepository(this._api);

  final ApiClient _api;

  Future<Paged<ErrorLogItem>> list({String search = '', String? source, int page = 1}) => _api.get(
    '/api/error-logs',
    (json) => Paged.fromJson(json, ErrorLogItem.fromJson),
    query: {'search': search.trim(), 'source': source, 'page': page, 'pageSize': pageSize},
  );

  Future<ErrorLogItem> get(int id) => _api.get('/api/error-logs/$id', ErrorLogItem.fromJson);
}
