import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/realtime.dart';
import '../../core/theme.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/notification.dart';
import '../../data/models/user.dart';

class NavItem {
  const NavItem(this.path, this.label, this.icon, {this.adminOnly = false});

  final String path;
  final String label;
  final IconData icon;
  final bool adminOnly;
}

/// Everything in the navigation, in order. Administrator-only entries are not shown to
/// employees, and the router refuses their paths as well.
const navItems = [
  NavItem('/dashboard', 'Dashboard', Icons.space_dashboard_outlined),
  NavItem('/appointments', 'Rezervacije', Icons.event_note_outlined),
  NavItem('/services', 'Usluge', Icons.build_outlined, adminOnly: true),
  NavItem('/employees', 'Zaposlenici', Icons.engineering_outlined, adminOnly: true),
  NavItem('/users', 'Korisnici', Icons.people_outline, adminOnly: true),
  NavItem('/payments', 'Plaćanja', Icons.payments_outlined, adminOnly: true),
  NavItem('/reviews', 'Recenzije', Icons.reviews_outlined, adminOnly: true),
  NavItem('/reports', 'Izvještaji', Icons.bar_chart_outlined, adminOnly: true),
  NavItem('/reference', 'Šifarnici', Icons.list_alt_outlined, adminOnly: true),
  NavItem('/error-log', 'Log grešaka', Icons.bug_report_outlined, adminOnly: true),
  NavItem('/notifications', 'Obavijesti', Icons.notifications_outlined),
  NavItem('/profile', 'Moj profil', Icons.account_circle_outlined),
];

class AppShell extends ConsumerWidget {
  const AppShell({required this.location, required this.child, super.key});

  final String location;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Keeps the live connection open while the signed-in part of the application is shown.
    ref.watch(notificationRealtimeProvider);

    ref.listen<AppNotification?>(incomingNotificationProvider, (_, notification) {
      if (notification == null) {
        return;
      }
      final appointmentId = notification.appointmentId;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            backgroundColor: AppColors.surfaceRaised,
            content: Text('${notification.title}\n${notification.body}'),
            action: appointmentId == null
                ? SnackBarAction(label: 'Obavijesti', onPressed: () => context.go('/notifications'))
                : SnackBarAction(label: 'Otvori', onPressed: () => context.go('/appointments/$appointmentId')),
          ),
        );
    });

    final user = ref.watch(authProvider).value;
    final isAdministrator = user?.isAdministrator ?? false;
    final items = [
      for (final item in navItems)
        if (isAdministrator || !item.adminOnly) item,
    ];
    final selected = items.indexWhere((item) => location.startsWith(item.path));

    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            extended: MediaQuery.sizeOf(context).width >= 1200,
            minExtendedWidth: 220,
            selectedIndex: selected < 0 ? null : selected,
            onDestinationSelected: (index) => context.go(items[index].path),
            leading: const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Icon(Icons.directions_car_filled_outlined, color: AppColors.accent, size: 36),
            ),
            destinations: [
              for (final item in items)
                NavigationRailDestination(
                  icon: item.path == '/notifications' ? _Bell(icon: item.icon) : Icon(item.icon),
                  label: Text(item.label),
                ),
            ],
          ),
          const VerticalDivider(width: 1),
          Expanded(
            child: Column(
              children: [
                _TopBar(user: user, location: location),
                const Divider(),
                Expanded(child: child),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Bell extends ConsumerWidget {
  const _Bell({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadCountProvider).value ?? 0;
    return Badge(isLabelVisible: unread > 0, label: Text('$unread'), child: Icon(icon));
  }
}

class _TopBar extends ConsumerWidget {
  const _TopBar({required this.user, required this.location});

  final User? user;
  final String location;

  /// One level up from a detail screen, or null on a top-level screen.
  String? get parent {
    final segments = Uri.parse(location).pathSegments;
    return segments.length > 1 ? '/${segments.sublist(0, segments.length - 1).join('/')}' : null;
  }

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirm(
      context,
      title: 'Odjaviti se?',
      message: 'Sesija će biti završena i na serveru.',
      confirmLabel: 'Odjavi se',
    );
    if (confirmed) {
      await ref.read(authProvider.notifier).logout();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final current = user;
    final unread = ref.watch(unreadCountProvider).value ?? 0;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
      child: Row(
        children: [
          // Back is always shown and disabled only where there is nowhere to go back to. Detail
          // screens live under their list's path (/appointments/656), so back is one level up -
          // the same answer whichever screen the detail was opened from.
          IconButton(
            tooltip: 'Nazad',
            onPressed: parent == null ? null : () => context.go(parent!),
            icon: const Icon(Icons.arrow_back),
          ),
          const Spacer(),
          IconButton(
            tooltip: 'Obavijesti',
            onPressed: () => context.go('/notifications'),
            icon: Badge(
              isLabelVisible: unread > 0,
              label: Text('$unread'),
              child: const Icon(Icons.notifications_outlined),
            ),
          ),
          const SizedBox(width: 12),
          if (current != null) ...[
            CircleAvatar(
              backgroundColor: AppColors.accentSoft,
              child: Text(
                current.initials,
                style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.w700),
              ),
            ),
            const SizedBox(width: 10),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(current.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
                Text(
                  current.roles.map(Roles.label).join(', '),
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                ),
              ],
            ),
          ],
          const SizedBox(width: 12),
          IconButton(tooltip: 'Odjava', onPressed: () => _logout(context, ref), icon: const Icon(Icons.logout)),
        ],
      ),
    );
  }
}
