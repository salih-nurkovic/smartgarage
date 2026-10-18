import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/api/api_exception.dart';
import '../core/providers.dart';
import '../core/widgets/common.dart';
import '../data/models/user.dart';
import '../features/appointments/appointment_detail_screen.dart';
import '../features/appointments/appointments_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/dashboard/dashboard_screen.dart';
import '../features/employees/employees_screen.dart';
import '../features/error_log/error_log_screen.dart';
import '../features/notifications/notifications_screen.dart';
import '../features/payments/payments_screen.dart';
import '../features/profile/profile_screen.dart';
import '../features/reference/reference_screen.dart';
import '../features/reviews/reviews_screen.dart';
import '../features/services/services_screen.dart';
import '../features/shell/app_shell.dart';
import '../features/users/users_screen.dart';

final _adminPaths = [
  for (final item in navItems)
    if (item.adminOnly) item.path,
];

final routerProvider = Provider<GoRouter>((ref) {
  // Re-checks where the user may be whenever the session changes.
  final session = ValueNotifier<AsyncValue<User?>>(const AsyncLoading());
  ref
    ..listen(authProvider, (_, next) => session.value = next, fireImmediately: true)
    ..onDispose(session.dispose);

  return GoRouter(
    initialLocation: '/dashboard',
    refreshListenable: session,
    redirect: (context, state) {
      final auth = session.value;
      final path = state.matchedLocation;

      if (!auth.hasValue) {
        return path == '/splash' ? null : '/splash';
      }
      final user = auth.value;
      if (user == null) {
        return path == '/login' ? null : '/login';
      }
      if (path == '/login' || path == '/splash') {
        return '/dashboard';
      }
      // The API refuses these to employees anyway; the application does not offer them either.
      if (!user.isAdministrator && _adminPaths.any(path.startsWith)) {
        return '/dashboard';
      }
      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const _SplashScreen()),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      ShellRoute(
        builder: (_, state, child) => AppShell(location: state.uri.path, child: child),
        routes: [
          GoRoute(path: '/dashboard', builder: (_, _) => const DashboardScreen()),
          GoRoute(
            path: '/appointments',
            builder: (_, _) => const AppointmentsScreen(),
            routes: [
              GoRoute(
                path: ':id',
                builder: (_, state) => AppointmentDetailScreen(appointmentId: int.parse(state.pathParameters['id']!)),
              ),
            ],
          ),
          GoRoute(path: '/services', builder: (_, _) => const ServicesScreen()),
          GoRoute(path: '/employees', builder: (_, _) => const EmployeesScreen()),
          GoRoute(path: '/users', builder: (_, _) => const UsersScreen()),
          GoRoute(path: '/payments', builder: (_, _) => const PaymentsScreen()),
          GoRoute(path: '/reviews', builder: (_, _) => const ReviewsScreen()),
          GoRoute(path: '/reference', builder: (_, _) => const ReferenceScreen()),
          GoRoute(path: '/error-log', builder: (_, _) => const ErrorLogScreen()),
          GoRoute(path: '/notifications', builder: (_, _) => const NotificationsScreen()),
          GoRoute(path: '/profile', builder: (_, _) => const ProfileScreen()),
        ],
      ),
    ],
  );
});

/// Shown while the saved session is restored, or when the server could not be reached to
/// restore it.
class _SplashScreen extends ConsumerWidget {
  const _SplashScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authProvider);
    return Scaffold(
      body: auth.hasError
          ? ErrorView(message: errorMessage(auth.error!), onRetry: () => ref.invalidate(authProvider))
          : const Center(child: CircularProgressIndicator()),
    );
  }
}
