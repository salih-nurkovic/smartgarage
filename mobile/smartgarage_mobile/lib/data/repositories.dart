import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../core/api/api_client.dart';
import '../core/config.dart';
import 'models/appointment.dart';
import 'models/common.dart';
import 'models/lookup.dart';
import 'models/notification.dart';
import 'models/payment.dart';
import 'models/recommendation.dart';
import 'models/review.dart';
import 'models/service.dart';
import 'models/user.dart';
import 'models/vehicle.dart';

class AuthRepository {
  const AuthRepository(this._api);

  final ApiClient _api;

  Future<AuthResult> login(String userName, String password) => _api.anonymous(
        '/api/auth/login',
        AuthResult.fromJson,
        body: {'userName': userName, 'password': password},
      );

  Future<AuthResult> register({
    required String userName,
    required String email,
    required String firstName,
    required String lastName,
    required String phoneNumber,
    required String password,
    required String confirmPassword,
  }) =>
      _api.anonymous('/api/auth/register', AuthResult.fromJson, body: {
        'userName': userName,
        'email': email,
        'firstName': firstName,
        'lastName': lastName,
        'phoneNumber': phoneNumber,
        'password': password,
        'confirmPassword': confirmPassword,
      });

  Future<void> forgotPassword(String userNameOrEmail) =>
      _api.anonymous('/api/auth/forgot-password', (_) {}, body: {'userNameOrEmail': userNameOrEmail});

  Future<void> resetPassword({
    required String userNameOrEmail,
    required String code,
    required String newPassword,
    required String confirmNewPassword,
  }) =>
      _api.anonymous('/api/auth/reset-password', (_) {}, body: {
        'userNameOrEmail': userNameOrEmail,
        'code': code,
        'newPassword': newPassword,
        'confirmNewPassword': confirmNewPassword,
      });

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) =>
      _api.post('/api/auth/change-password', (_) {}, body: {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
        'confirmNewPassword': confirmNewPassword,
      });

  /// Ends the session on the server, not only on the phone.
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
  }) =>
      _api.put('/api/profile', (json) => User.fromJson(json as Json), body: {
        'email': email,
        'firstName': firstName,
        'lastName': lastName,
        'phoneNumber': phoneNumber,
      });

  Future<Uint8List> image() => _api.bytes('/api/profile/image');

  Future<User> uploadImage(Uint8List bytes, String fileName) => _api.post(
        '/api/profile/image',
        (json) => User.fromJson(json as Json),
        body: FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: fileName)}),
      );
}

class CatalogRepository {
  const CatalogRepository(this._api);

  final ApiClient _api;

  Future<Lookup> lookup() => _api.get('/api/lookup', Lookup.fromJson);

  Future<Paged<ServiceItem>> services(ServiceFilter filter, {int page = 1, int pageSize = AppConfig.pageSize}) => _api.get(
        '/api/services',
        (json) => Paged.fromJson(json, ServiceItem.fromJson),
        query: {...filter.toQuery(), 'page': page, 'pageSize': pageSize},
      );

  /// Opening a service is recorded on the server as interest in it, for the recommender.
  Future<ServiceItem> service(int id) => _api.get('/api/services/$id', (json) => ServiceItem.fromJson(json as Json));

  Future<Uint8List> image(String url) => _api.bytes(url);
}

class VehicleRepository {
  const VehicleRepository(this._api);

  final ApiClient _api;

  /// A customer has a handful of vehicles, so one full page covers them all.
  Future<List<Vehicle>> mine() =>
      _api.get('/api/vehicles', (json) => Paged.fromJson(json, Vehicle.fromJson).items, query: {'pageSize': 100});

  Future<Vehicle> create(VehicleRequest request) =>
      _api.post('/api/vehicles', (json) => Vehicle.fromJson(json as Json), body: request.toJson());

  Future<Vehicle> update(int id, VehicleRequest request) =>
      _api.put('/api/vehicles/$id', (json) => Vehicle.fromJson(json as Json), body: request.toJson());

  Future<void> delete(int id) => _api.delete('/api/vehicles/$id');
}

class AppointmentRepository {
  const AppointmentRepository(this._api);

  final ApiClient _api;

  Future<Paged<AppointmentListItem>> list({AppointmentStatus? status, int page = 1, int pageSize = AppConfig.pageSize}) =>
      _api.get(
        '/api/appointments',
        (json) => Paged.fromJson(json, AppointmentListItem.fromJson),
        query: {'status': status?.apiName, 'page': page, 'pageSize': pageSize},
      );

  Future<AppointmentSummary> summary() => _api.get('/api/appointments/summary', AppointmentSummary.fromJson);

  Future<AppointmentDetail> detail(int id) => _api.get('/api/appointments/$id', AppointmentDetail.fromJson);

  Future<List<Slot>> slots(DateTime day, List<int> serviceIds) => _api.get(
        '/api/availability/slots',
        (json) => toList(json, Slot.fromJson),
        query: {
          'date': '${day.year}-${_two(day.month)}-${_two(day.day)}',
          'serviceIds': serviceIds,
        },
      );

  Future<AppointmentDetail> create({
    required int vehicleId,
    required List<int> serviceIds,
    required DateTime startUtc,
    String? notes,
  }) =>
      _api.post('/api/appointments', AppointmentDetail.fromJson, body: {
        'vehicleId': vehicleId,
        'serviceIds': serviceIds,
        'scheduledStartUtc': startUtc.toIso8601String(),
        'customerNotes': notes,
      });

  Future<AppointmentDetail> cancel(int id, String reason) =>
      _api.post('/api/appointments/$id/cancel', AppointmentDetail.fromJson, body: {'reason': reason});

  Future<AppointmentDetail> reschedule(int id, DateTime newStartUtc, String? reason) => _api.put(
        '/api/appointments/$id/reschedule',
        AppointmentDetail.fromJson,
        body: {'newStartUtc': newStartUtc.toIso8601String(), 'reason': reason},
      );

  static String _two(int value) => value.toString().padLeft(2, '0');
}

class PaymentRepository {
  const PaymentRepository(this._api);

  final ApiClient _api;

  /// Only the appointment is sent; the server works out the amount from its own records.
  Future<PaymentIntent> createIntent(int appointmentId) =>
      _api.post('/api/payments/intent', PaymentIntent.fromJson, body: {'appointmentId': appointmentId});

  /// Asks the server for the payment; it checks the status with Stripe before answering.
  Future<Payment> get(int id) => _api.get('/api/payments/$id', Payment.fromJson);

  Future<Paged<Payment>> mine({int page = 1}) => _api.get(
        '/api/payments',
        (json) => Paged.fromJson(json, Payment.fromJson),
        query: {'page': page, 'pageSize': AppConfig.pageSize},
      );
}

class ReviewRepository {
  const ReviewRepository(this._api);

  final ApiClient _api;

  Future<Paged<Review>> list({int? serviceId, bool mine = false, int page = 1}) => _api.get(
        '/api/reviews',
        (json) => Paged.fromJson(json, Review.fromJson),
        query: {'serviceId': serviceId, 'mine': mine, 'page': page, 'pageSize': AppConfig.pageSize},
      );

  Future<ReviewSummary> summary({int? serviceId}) =>
      _api.get('/api/reviews/summary', ReviewSummary.fromJson, query: {'serviceId': serviceId});

  Future<List<ReviewableAppointment>> reviewable() =>
      _api.get('/api/reviews/reviewable', (json) => toList(json, ReviewableAppointment.fromJson));

  Future<void> create({required int appointmentId, required int serviceId, required int rating, String? comment}) =>
      _api.post('/api/reviews', (_) {}, body: {
        'appointmentId': appointmentId,
        'serviceId': serviceId,
        'rating': rating,
        'comment': comment,
      });

  Future<void> delete(int id) => _api.delete('/api/reviews/$id');
}

class NotificationRepository {
  const NotificationRepository(this._api);

  final ApiClient _api;

  Future<Paged<AppNotification>> list({int page = 1}) => _api.get(
        '/api/notifications',
        (json) => Paged.fromJson(json, AppNotification.fromJson),
        query: {'page': page, 'pageSize': AppConfig.pageSize},
      );

  Future<int> unreadCount() => _api.get('/api/notifications/unread-count', (json) => (json as Json)['count'] as int);

  Future<void> markRead(int id) => _api.patch('/api/notifications/$id/read', (_) {});

  Future<void> markAllRead() => _api.patch('/api/notifications/read-all', (_) {});

  Future<void> delete(int id) => _api.delete('/api/notifications/$id');
}

class RecommendationRepository {
  const RecommendationRepository(this._api);

  final ApiClient _api;

  Future<Recommendations> forVehicle(int vehicleId) =>
      _api.get('/api/recommendations', Recommendations.fromJson, query: {'vehicleId': vehicleId});

  Future<void> clicked(int serviceId, int vehicleId) =>
      _api.post('/api/recommendations/$serviceId/clicked', (_) {}, body: {'vehicleId': vehicleId});

  Future<void> markDone(int serviceId, int vehicleId) =>
      _api.post('/api/recommendations/$serviceId/done', (_) {}, body: {'vehicleId': vehicleId});
}
