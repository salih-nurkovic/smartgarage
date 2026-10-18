import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/lookup.dart';
import '../data/models/user.dart';
import '../data/repositories.dart';
import 'api/api_client.dart';
import 'api/api_exception.dart';
import 'api/token_storage.dart';
import 'formatting.dart';

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient(ref.watch(tokenStorageProvider)));

final authRepositoryProvider = Provider((ref) => AuthRepository(ref.watch(apiClientProvider)));
final profileRepositoryProvider = Provider((ref) => ProfileRepository(ref.watch(apiClientProvider)));
final dashboardRepositoryProvider = Provider((ref) => DashboardRepository(ref.watch(apiClientProvider)));
final appointmentRepositoryProvider = Provider((ref) => AppointmentRepository(ref.watch(apiClientProvider)));
final serviceRepositoryProvider = Provider((ref) => ServiceRepository(ref.watch(apiClientProvider)));
final employeeRepositoryProvider = Provider((ref) => EmployeeRepository(ref.watch(apiClientProvider)));
final userRepositoryProvider = Provider((ref) => UserRepository(ref.watch(apiClientProvider)));
final paymentRepositoryProvider = Provider((ref) => PaymentRepository(ref.watch(apiClientProvider)));
final reviewRepositoryProvider = Provider((ref) => ReviewRepository(ref.watch(apiClientProvider)));
final referenceRepositoryProvider = Provider((ref) => ReferenceRepository(ref.watch(apiClientProvider)));
final notificationRepositoryProvider = Provider((ref) => NotificationRepository(ref.watch(apiClientProvider)));
final errorLogRepositoryProvider = Provider((ref) => ErrorLogRepository(ref.watch(apiClientProvider)));

/// The signed-in staff member, or null. Restores the saved session at start-up.
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
      return user.isStaff ? user : await _endSession(tokens);
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
    final tokens = ref.read(tokenStorageProvider);
    await tokens.save(result.accessToken, result.refreshToken);

    if (!result.user.isStaff) {
      await _endSession(tokens);
      throw ApiException('Desktop aplikacija je namijenjena osoblju servisa. Kupci koriste mobilnu aplikaciju.');
    }
    state = AsyncData(result.user);
  }

  /// Invalidates the session on the server first; the application forgets the tokens either way.
  Future<void> logout() async {
    try {
      await ref.read(authRepositoryProvider).logout();
    } finally {
      await ref.read(tokenStorageProvider).clear();
      state = const AsyncData(null);
    }
  }

  void updateUser(User user) => state = AsyncData(user);

  // Customer accounts can sign in to the API, but this application has nothing for them.
  Future<User?> _endSession(TokenStorage tokens) async {
    try {
      await ref.read(authRepositoryProvider).logout();
    } finally {
      await tokens.clear();
    }
    return null;
  }
}

/// Changes whenever a different person signs in, so cached data is never shown across sessions.
final sessionUserProvider = Provider<String?>((ref) => ref.watch(authProvider.select((s) => s.value?.userName)));

final lookupProvider = FutureProvider<Lookup>((ref) {
  ref.watch(sessionUserProvider);
  ref.watch(referenceVersionProvider);
  return ref.watch(dashboardRepositoryProvider).lookup();
});

final moneyProvider = Provider<MoneyFormat>(
  (ref) => MoneyFormat(ref.watch(lookupProvider.select((l) => l.value?.garage.currency)) ?? 'EUR'),
);

/// Bumped when appointments change - here, or as announced by a live notification - so every
/// view of them reloads.
final appointmentsVersionProvider = NotifierProvider<Version, int>(Version.new);

/// Bumped when a notification arrives or the inbox changes.
final notificationsVersionProvider = NotifierProvider<Version, int>(Version.new);

/// Bumped when a reference table changes, so dropdowns built from the lookup reload.
final referenceVersionProvider = NotifierProvider<Version, int>(Version.new);

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
