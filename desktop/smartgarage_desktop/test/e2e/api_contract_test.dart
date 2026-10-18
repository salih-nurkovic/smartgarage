@Tags(['e2e'])
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:smartgarage_desktop/core/api/api_client.dart';
import 'package:smartgarage_desktop/core/api/api_exception.dart';
import 'package:smartgarage_desktop/core/api/token_storage.dart';
import 'package:smartgarage_desktop/data/models/appointment.dart';
import 'package:smartgarage_desktop/data/models/common.dart';
import 'package:smartgarage_desktop/data/repositories.dart';

/// Runs the application's own repositories against a running API with the seeded data, so every
/// model is checked against what the server really sends, for both staff roles. It changes data
/// (a tag, a user, an appointment), so it only runs when asked:
///
///     flutter test test/e2e --dart-define=E2E=true --dart-define=API_BASE_URL=http://localhost:5000
const _enabled = bool.fromEnvironment('E2E');

/// Tokens in memory; the platform keystore is not available to a test.
class _MemoryTokens extends TokenStorage {
  String? _access;
  String? _refresh;

  @override
  String? get accessToken => _access;

  @override
  String? get refreshToken => _refresh;

  @override
  bool get hasSession => _refresh != null;

  @override
  Future<void> load() async {}

  @override
  Future<void> save(String accessToken, String refreshToken) async {
    _access = accessToken;
    _refresh = refreshToken;
  }

  @override
  Future<void> clear() async {
    _access = null;
    _refresh = null;
  }
}

Future<ApiClient> _signIn(String userName) async {
  final tokens = _MemoryTokens();
  final api = ApiClient(tokens);
  final session = await AuthRepository(api).login(userName, 'test');
  await tokens.save(session.accessToken, session.refreshToken);
  return api;
}

void main() {
  group('API contract', skip: _enabled ? false : 'Pokreće se uz --dart-define=E2E=true i pokrenut API.', () {
    late ApiClient admin;
    late ApiClient employee;
    late ApiClient customer;

    setUpAll(() async {
      await initializeDateFormatting('bs');
      admin = await _signIn('admin');
      employee = await _signIn('employee');
      customer = await _signIn('mobile');
    });

    test('every administrator screen reads what the server sends', () async {
      final dashboard = await DashboardRepository(admin).get();
      expect(dashboard.monthRevenue, isNotNull);
      expect(dashboard.revenueByMonth, hasLength(6));

      final lookup = await DashboardRepository(admin).lookup();
      expect(lookup.tags, isNotEmpty);

      final appointments = AppointmentRepository(admin);
      final page = await appointments.list(const AppointmentQuery(status: AppointmentStatus.completed));
      expect(page.items.every((a) => a.status == AppointmentStatus.completed), isTrue);
      expect((await appointments.summary()).total, greaterThan(0));
      final detail = await appointments.detail(page.items.first.id);
      expect(detail.items, isNotEmpty);
      expect(await appointments.upcoming(DateTime.now().toUtc()), isA<List<AppointmentListItem>>());

      expect((await ServiceRepository(admin).list()).items.first.bookingCount, isNonNegative);
      expect((await EmployeeRepository(admin).list()).items.first.userId, isPositive);
      expect((await UserRepository(admin).list(role: 'Customer')).items, isNotEmpty);

      final payments = PaymentRepository(admin);
      final paid = await payments.list();
      await payments.summary();
      await payments.get(paid.items.first.id);

      await ReviewRepository(admin).list(rating: 5);
      final reference = ReferenceRepository(admin);
      for (final path in [
        '/api/service-categories',
        '/api/service-tags',
        '/api/vehicle-brands',
        '/api/vehicle-models',
        '/api/engine-types',
      ]) {
        expect((await reference.list(path)).items, isNotEmpty, reason: path);
      }

      await NotificationRepository(admin).list();
      final errors = await ErrorLogRepository(admin).list();
      if (errors.items.isNotEmpty) {
        await ErrorLogRepository(admin).get(errors.items.first.id);
      }
    });

    test('an employee gets the dashboard without revenue, and nothing for administrators', () async {
      final dashboard = await DashboardRepository(employee).get();
      expect(dashboard.monthRevenue, isNull);
      expect(dashboard.revenueByMonth, isNull);

      await expectLater(
        UserRepository(employee).list(),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 403)),
      );
      await expectLater(
        PaymentRepository(employee).summary(),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 403)),
      );
    });

    test('a reference row is added, changed and deleted', () async {
      final reference = ReferenceRepository(admin);
      final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);

      await reference.create('/api/service-tags', {'name': 'E2E $stamp', 'slug': 'e2e-$stamp', 'isActive': true});
      final created = (await reference.list('/api/service-tags', search: 'E2E $stamp')).items.single;
      expect(created['slug'], 'e2e-$stamp');

      // The same slug twice is refused under the slug field.
      try {
        await reference.create('/api/service-tags', {
          'name': 'E2E drugi $stamp',
          'slug': 'e2e-$stamp',
          'isActive': true,
        });
        fail('a duplicate slug must be refused');
      } on ApiException catch (error) {
        expect(error.fieldErrors.keys, contains('slug'));
      }

      await reference.update('/api/service-tags', created.id, {
        'name': 'E2E izmijenjen $stamp',
        'slug': 'e2e-$stamp',
        'isActive': false,
      });
      await reference.delete('/api/service-tags', created.id);
      expect((await reference.list('/api/service-tags', search: 'E2E $stamp')).items, isEmpty);
    });

    test('an account is created, deactivated with a reason and activated again', () async {
      final users = UserRepository(admin);
      final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(6);

      final created = await users.create({
        'userName': 'e2e$stamp',
        'email': 'e2e$stamp@smartgarage.ba',
        'firstName': 'Test',
        'lastName': 'Korisnik',
        'phoneNumber': '+387 61 000 000',
        'password': 'Sigurna#2026',
        'confirmPassword': 'Sigurna#2026',
        'role': 'Customer',
      });
      expect(created.isActive, isTrue);

      expect((await users.setStatus(created.id, isActive: false, reason: 'Ugovorni test desktopa.')).isActive, isFalse);
      expect((await users.setStatus(created.id, isActive: true)).isActive, isTrue);
    });

    test('staff confirm, assign, reschedule and cancel a customer booking', () async {
      final vehicle = await customer.get('/api/vehicles', (json) => Paged.fromJson(json, (v) => v).items.first);
      final appointments = AppointmentRepository(admin);
      final serviceId = (await ServiceRepository(admin).list(isActive: true)).items.first.id;

      Future<List<Slot>> freeOn(DateTime day) async =>
          (await appointments.slots(day, [serviceId])).where((s) => s.isAvailable).toList();

      // Two free slots a few weeks out, so the change-window rules do not get in the way.
      var day = DateTime.now().toUtc().add(const Duration(days: 21));
      var free = <Slot>[];
      for (var tries = 0; free.length < 2 && tries < 14; tries++) {
        day = DateTime.utc(day.year, day.month, day.day).add(const Duration(days: 1));
        free = await freeOn(day);
      }
      expect(free.length, greaterThanOrEqualTo(2));

      final booked = await customer.post(
        '/api/appointments',
        AppointmentDetail.fromJson,
        body: {
          'vehicleId': vehicle['id'],
          'serviceIds': [serviceId],
          'scheduledStartUtc': free[0].startUtc.toIso8601String(),
        },
      );
      expect(booked.status, AppointmentStatus.pending);

      final confirmed = await appointments.confirm(booked.id);
      expect(confirmed.status, AppointmentStatus.confirmed);

      final mechanic = (await EmployeeRepository(admin).list(isActive: true)).items
          .firstWhere((e) => e.isAvailableForAssignment);
      final assigned = await appointments.assign(booked.id, mechanic.id);
      expect(assigned.assignedEmployeeName, mechanic.fullName);

      final moved = await appointments.reschedule(booked.id, free[1].startUtc, 'Ugovorni test desktopa.');
      expect(moved.startUtc, free[1].startUtc);
      expect(moved.status, AppointmentStatus.confirmed);

      final cancelled = await appointments.cancel(booked.id, 'Ugovorni test desktopa, termin nije potreban.');
      expect(cancelled.status, AppointmentStatus.cancelled);
      expect(
        cancelled.history.map((h) => h.toStatus),
        containsAll([AppointmentStatus.confirmed, AppointmentStatus.cancelled]),
      );
    });
  });
}
