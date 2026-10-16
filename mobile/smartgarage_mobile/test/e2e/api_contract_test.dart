@Tags(['e2e'])
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:smartgarage_mobile/core/api/api_client.dart';
import 'package:smartgarage_mobile/core/api/api_exception.dart';
import 'package:smartgarage_mobile/core/api/token_storage.dart';
import 'package:smartgarage_mobile/data/models/appointment.dart';
import 'package:smartgarage_mobile/data/models/service.dart';
import 'package:smartgarage_mobile/data/models/vehicle.dart';
import 'package:smartgarage_mobile/data/repositories.dart';

/// Runs the app's own repositories against a running API with the seeded data, so every model
/// is checked against what the server really sends. It changes data (books and cancels an
/// appointment, adds and deletes a vehicle), so it only runs when asked:
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

void main() {
  group('API contract', skip: _enabled ? false : 'Pokreće se uz --dart-define=E2E=true i pokrenut API.', () {
    final tokens = _MemoryTokens();
    final api = ApiClient(tokens);
    final auth = AuthRepository(api);

    setUpAll(() async {
      await initializeDateFormatting('bs');
      final session = await auth.login('amar', 'test');
      await tokens.save(session.accessToken, session.refreshToken);
    });

    test('reference data, catalogue and reviews', () async {
      final catalog = CatalogRepository(api);
      final reviews = ReviewRepository(api);

      final lookup = await catalog.lookup();
      expect(lookup.garage.name, isNotEmpty);
      expect(lookup.brands.first.models, isNotEmpty);

      final services = await catalog.services(const ServiceFilter(sort: ServiceSort.priceAscending), pageSize: 100);
      expect(services.items, isNotEmpty);
      expect(services.items.first.price, lessThanOrEqualTo(services.items.last.price));

      final searched = await catalog.services(const ServiceFilter(search: 'zamjena'));
      expect(
        searched.items.every(
          (s) => s.name.toLowerCase().contains('zamjena') || s.description.toLowerCase().contains('zamjena'),
        ),
        isTrue,
      );

      final detail = await catalog.service(services.items.first.id);
      expect(detail.name, services.items.first.name);

      final summary = await reviews.summary();
      expect(summary.totalCount, greaterThan(0));
      await reviews.list();
      await reviews.list(mine: true);
      await reviews.reviewable();
    });

    test('profile, vehicles, appointments, payments, notifications and recommendations', () async {
      final profile = await ProfileRepository(api).get();
      expect(profile.isCustomer, isTrue);

      final vehicles = await VehicleRepository(api).mine();
      expect(vehicles, isNotEmpty);

      final appointments = AppointmentRepository(api);
      final summary = await appointments.summary();
      final page = await appointments.list();
      expect(page.totalCount, summary.total);

      final completed = await appointments.list(status: AppointmentStatus.completed);
      expect(completed.items.every((a) => a.status == AppointmentStatus.completed), isTrue);

      final detail = await appointments.detail(completed.items.first.id);
      expect(detail.items, isNotEmpty);
      expect(detail.history, isNotEmpty);

      await PaymentRepository(api).mine();

      final notifications = NotificationRepository(api);
      await notifications.list();
      await notifications.unreadCount();

      final recommendations = await RecommendationRepository(api).forVehicle(vehicles.first.id);
      expect(recommendations.algorithm, 'Content-Based');
      expect(recommendations.items.every((i) => i.reasons.isNotEmpty), isTrue);
    });

    test('book a free slot, see it, then cancel it with a reason', () async {
      final appointments = AppointmentRepository(api);
      final vehicle = (await VehicleRepository(api).mine()).first;
      final service = (await CatalogRepository(
        api,
      ).services(const ServiceFilter(), pageSize: 100)).items.firstWhere((s) => s.suitsEngine(vehicle.engineTypeId));

      // A free slot a couple of weeks out, so the 24-hour cancellation limit does not apply.
      Slot? free;
      var day = DateTime.now().toUtc().add(const Duration(days: 14));
      for (var tries = 0; free == null && tries < 14; tries++, day = day.add(const Duration(days: 1))) {
        final slots = await appointments.slots(DateTime.utc(day.year, day.month, day.day), [service.id]);
        free = slots.where((s) => s.isAvailable).firstOrNull;
      }
      expect(free, isNotNull, reason: 'no free slot in two weeks');

      final created = await appointments.create(
        vehicleId: vehicle.id,
        serviceIds: [service.id],
        startUtc: free!.startUtc,
        notes: 'Ugovorni test mobilne aplikacije',
      );
      expect(created.status, AppointmentStatus.pending);
      expect(created.startUtc, free.startUtc);
      expect(created.totalPrice, service.price);
      expect(created.can(AppointmentActions.cancel), isTrue);

      // The same slot again for the same vehicle is refused by the server.
      await expectLater(
        appointments.create(vehicleId: vehicle.id, serviceIds: [service.id], startUtc: free.startUtc),
        throwsA(isA<ApiException>()),
      );

      final cancelled = await appointments.cancel(created.id, 'Ugovorni test, termin nije potreban.');
      expect(cancelled.status, AppointmentStatus.cancelled);
      expect(cancelled.cancellationReason, 'Ugovorni test, termin nije potreban.');
    });

    test('add a vehicle, see a server field error, then delete it', () async {
      final vehicles = VehicleRepository(api);
      final lookup = await CatalogRepository(api).lookup();
      final brand = lookup.brands.first;
      final stamp = DateTime.now().millisecondsSinceEpoch.toString();

      final request = VehicleRequest(
        brandId: brand.id,
        modelId: brand.models.first.id,
        engineTypeId: lookup.engineTypes.first.id,
        productionYear: 2020,
        registrationPlate: 'E2E-${stamp.substring(stamp.length - 6)}',
        vin: 'E2ETEST${stamp.substring(stamp.length - 10)}',
        currentMileageKm: 1000,
      );

      final created = await vehicles.create(request);
      expect(created.registrationPlate, request.registrationPlate);

      try {
        await vehicles.create(request);
        fail('a duplicate VIN must be refused');
      } on ApiException catch (error) {
        expect(error.fieldErrors.keys, anyOf(contains('vin'), contains('registrationPlate')));
      }

      await vehicles.delete(created.id);
    });

    test('logout ends the session on the server', () async {
      final accessToken = tokens.accessToken!;
      await auth.logout();

      // The same access token is no longer accepted, and nothing renews it.
      final stale = _MemoryTokens();
      await stale.save(accessToken, 'already-revoked');
      final error = await ProfileRepository(ApiClient(stale))
          .get()
          .then<Object?>((_) => null, onError: (Object e) => e);
      expect(error, isA<ApiException>().having((e) => e.statusCode, 'status', 401));
    });
  });
}
