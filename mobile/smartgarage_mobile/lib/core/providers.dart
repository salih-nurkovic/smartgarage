import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/appointment.dart';
import '../data/models/lookup.dart';
import '../data/models/user.dart';
import '../data/models/vehicle.dart';
import '../data/repositories.dart';
import 'api/api_client.dart';
import 'api/api_exception.dart';
import 'api/token_storage.dart';
import 'formatting.dart';

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient(ref.watch(tokenStorageProvider)));

final authRepositoryProvider = Provider((ref) => AuthRepository(ref.watch(apiClientProvider)));
final profileRepositoryProvider = Provider((ref) => ProfileRepository(ref.watch(apiClientProvider)));
final catalogRepositoryProvider = Provider((ref) => CatalogRepository(ref.watch(apiClientProvider)));
final vehicleRepositoryProvider = Provider((ref) => VehicleRepository(ref.watch(apiClientProvider)));
final appointmentRepositoryProvider = Provider((ref) => AppointmentRepository(ref.watch(apiClientProvider)));
final paymentRepositoryProvider = Provider((ref) => PaymentRepository(ref.watch(apiClientProvider)));
final reviewRepositoryProvider = Provider((ref) => ReviewRepository(ref.watch(apiClientProvider)));
final notificationRepositoryProvider = Provider((ref) => NotificationRepository(ref.watch(apiClientProvider)));
final recommendationRepositoryProvider = Provider((ref) => RecommendationRepository(ref.watch(apiClientProvider)));

/// The signed-in customer, or null. Restores the saved session at start-up.
final authProvider = AsyncNotifierProvider<AuthController, User?>(AuthController.new);

class AuthController extends AsyncNotifier<User?> {
  @override
  Future<User?> build() async {
    final tokens = ref.read(tokenStorageProvider);
    ref.read(apiClientProvider).onSessionExpired = () => state = const AsyncData(null);

    await tokens.load();
    if (!tokens.hasSession) {
      return null;
    }

    try {
      final user = await ref.read(profileRepositoryProvider).get();
      return user.isCustomer ? user : await _endSession(tokens);
    } on ApiException catch (error) {
      if (error.isUnauthorized) {
        await tokens.clear();
        return null;
      }
      rethrow;
    }
  }

  Future<void> login(String userName, String password) async {
    final result = await ref.read(authRepositoryProvider).login(userName, password);
    await _start(result);
  }

  Future<void> register({
    required String userName,
    required String email,
    required String firstName,
    required String lastName,
    required String phoneNumber,
    required String password,
    required String confirmPassword,
  }) async {
    final result = await ref.read(authRepositoryProvider).register(
          userName: userName,
          email: email,
          firstName: firstName,
          lastName: lastName,
          phoneNumber: phoneNumber,
          password: password,
          confirmPassword: confirmPassword,
        );
    await _start(result);
  }

  /// Invalidates the session on the server first; the phone forgets the tokens either way.
  Future<void> logout() async {
    try {
      await ref.read(authRepositoryProvider).logout();
    } finally {
      await ref.read(tokenStorageProvider).clear();
      state = const AsyncData(null);
    }
  }

  void updateUser(User user) => state = AsyncData(user);

  Future<void> _start(AuthResult result) async {
    final tokens = ref.read(tokenStorageProvider);
    await tokens.save(result.accessToken, result.refreshToken);

    if (!result.user.isCustomer) {
      await _endSession(tokens);
      throw ApiException('Mobilna aplikacija je namijenjena kupcima. Osoblje koristi desktop aplikaciju.');
    }

    state = AsyncData(result.user);
  }

  // Staff accounts can sign in to the API, but this app has nothing for them.
  Future<User?> _endSession(TokenStorage tokens) async {
    try {
      await ref.read(authRepositoryProvider).logout();
    } finally {
      await tokens.clear();
    }
    return null;
  }
}

/// Changes whenever a different customer signs in, so every per-user list below is reloaded
/// rather than shown from the previous session.
final sessionUserProvider = Provider<String?>((ref) => ref.watch(authProvider.select((s) => s.value?.userName)));

final lookupProvider = FutureProvider<Lookup>((ref) => ref.watch(catalogRepositoryProvider).lookup());

final moneyProvider = Provider<MoneyFormat>(
  (ref) => MoneyFormat(ref.watch(lookupProvider.select((l) => l.value?.garage.currency)) ?? 'EUR'),
);

final vehiclesProvider = FutureProvider<List<Vehicle>>((ref) {
  ref.watch(sessionUserProvider);
  return ref.watch(vehicleRepositoryProvider).mine();
});

final appointmentSummaryProvider = FutureProvider<AppointmentSummary>((ref) {
  ref.watch(sessionUserProvider);
  ref.watch(appointmentsVersionProvider);
  return ref.watch(appointmentRepositoryProvider).summary();
});

final recentServicesProvider = FutureProvider<List<AppointmentListItem>>((ref) async {
  ref.watch(sessionUserProvider);
  ref.watch(appointmentsVersionProvider);
  final page = await ref.watch(appointmentRepositoryProvider).list(status: AppointmentStatus.completed, pageSize: 3);
  return page.items;
});

final appointmentDetailProvider = FutureProvider.autoDispose.family<AppointmentDetail, int>((ref, id) {
  ref.watch(appointmentsVersionProvider);
  return ref.watch(appointmentRepositoryProvider).detail(id);
});

/// Bumped when something changes appointments behind the screens' backs - a booking made here,
/// or a status change announced by a live notification - so every appointment view reloads.
final appointmentsVersionProvider = NotifierProvider<Version, int>(Version.new);

/// Bumped when a notification arrives or the inbox is changed.
final notificationsVersionProvider = NotifierProvider<Version, int>(Version.new);

class Version extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final unreadCountProvider = FutureProvider<int>((ref) {
  ref.watch(sessionUserProvider);
  ref.watch(notificationsVersionProvider);
  return ref.watch(notificationRepositoryProvider).unreadCount();
});
