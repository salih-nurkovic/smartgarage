import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/providers.dart';
import '../data/models/user.dart';
import '../data/models/vehicle.dart';
import '../features/appointments/appointment_detail_screen.dart';
import '../features/appointments/history_screen.dart';
import '../features/appointments/reschedule_screen.dart';
import '../features/auth/forgot_password_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/auth/register_screen.dart';
import '../features/booking/booking_screen.dart';
import '../features/catalog/catalog_screen.dart';
import '../features/catalog/service_detail_screen.dart';
import '../features/home/home_screen.dart';
import '../features/notifications/notifications_screen.dart';
import '../features/payments/payments_screen.dart';
import '../features/profile/change_password_screen.dart';
import '../features/profile/edit_profile_screen.dart';
import '../features/profile/profile_screen.dart';
import '../features/recommendations/recommendations_screen.dart';
import '../features/reviews/review_form_screen.dart';
import '../features/reviews/reviews_screen.dart';
import '../features/shell/app_shell.dart';
import '../features/vehicles/vehicle_form_screen.dart';
import '../features/vehicles/vehicles_screen.dart';
import 'splash_screen.dart';

const _publicPaths = {'/login', '/register', '/forgot-password'};

int? _intParam(GoRouterState state, String name) => int.tryParse(state.uri.queryParameters[name] ?? '');

final routerProvider = Provider<GoRouter>((ref) {
  // The router re-checks where the user may be whenever the session changes: signing in,
  // signing out, or a refresh token the server no longer accepts.
  final session = ValueNotifier<AsyncValue<User?>>(const AsyncLoading());
  ref
    ..listen(authProvider, (_, next) => session.value = next, fireImmediately: true)
    ..onDispose(session.dispose);

  return GoRouter(
    initialLocation: '/home',
    refreshListenable: session,
    redirect: (context, state) {
      final auth = session.value;
      final path = state.matchedLocation;

      // Still restoring the session, or the server could not be reached to restore it.
      if (!auth.hasValue) {
        return path == '/splash' ? null : '/splash';
      }
      final signedIn = auth.value != null;
      if (!signedIn) {
        return _publicPaths.contains(path) ? null : '/login';
      }
      return _publicPaths.contains(path) || path == '/splash' ? '/home' : null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),
      GoRoute(path: '/forgot-password', builder: (_, _) => const ForgotPasswordScreen()),
      StatefulShellRoute.indexedStack(
        builder: (_, _, shell) => AppShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/home', builder: (_, _) => const HomeScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/services', builder: (_, _) => const CatalogScreen())]),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/book',
                // A new link (from a vehicle, a service or a recommendation) starts a new booking.
                builder: (_, state) => BookingScreen(
                  key: ValueKey(state.uri.toString()),
                  vehicleId: _intParam(state, 'vehicleId'),
                  serviceId: _intParam(state, 'serviceId'),
                ),
              ),
            ],
          ),
          StatefulShellBranch(routes: [GoRoute(path: '/history', builder: (_, _) => const HistoryScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/profile', builder: (_, _) => const ProfileScreen())]),
        ],
      ),
      GoRoute(
        path: '/appointments/:id',
        builder: (_, state) => AppointmentDetailScreen(appointmentId: int.parse(state.pathParameters['id']!)),
        routes: [
          GoRoute(
            path: 'reschedule',
            builder: (_, state) => RescheduleScreen(appointmentId: int.parse(state.pathParameters['id']!)),
          ),
        ],
      ),
      GoRoute(
        path: '/services/:id',
        builder: (_, state) => ServiceDetailScreen(serviceId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/vehicles',
        builder: (_, _) => const VehiclesScreen(),
        routes: [
          GoRoute(path: 'new', builder: (_, _) => const VehicleFormScreen()),
          GoRoute(path: ':id/edit', builder: (_, state) => VehicleFormScreen(vehicle: state.extra as Vehicle?)),
        ],
      ),
      GoRoute(
        path: '/recommendations',
        builder: (_, state) => RecommendationsScreen(vehicleId: _intParam(state, 'vehicleId')),
      ),
      GoRoute(path: '/notifications', builder: (_, _) => const NotificationsScreen()),
      GoRoute(path: '/payments', builder: (_, _) => const PaymentsScreen()),
      GoRoute(
        path: '/reviews',
        builder: (_, state) => ReviewsScreen(serviceId: _intParam(state, 'serviceId')),
        routes: [
          GoRoute(
            path: 'new',
            builder: (_, state) => ReviewFormScreen(appointmentId: _intParam(state, 'appointmentId')),
          ),
        ],
      ),
      GoRoute(path: '/profile/edit', builder: (_, _) => const EditProfileScreen()),
      GoRoute(path: '/profile/password', builder: (_, _) => const ChangePasswordScreen()),
    ],
  );
});
