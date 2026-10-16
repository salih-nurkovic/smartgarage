import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/realtime.dart';
import '../../core/theme.dart';
import '../../data/models/notification.dart';

/// The five tabs agreed for the mockups: Početna, Servisi, Zakaži, Historija, Profil.
class AppShell extends ConsumerWidget {
  const AppShell({required this.shell, super.key});

  final StatefulNavigationShell shell;

  static const _tabs = [
    (Icons.home_outlined, Icons.home, 'Početna'),
    (Icons.build_outlined, Icons.build, 'Servisi'),
    (Icons.calendar_month_outlined, Icons.calendar_month, 'Zakaži'),
    (Icons.history, Icons.history, 'Historija'),
    (Icons.person_outline, Icons.person, 'Profil'),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Keeps the live connection open for as long as the signed-in part of the app is shown.
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
                ? SnackBarAction(label: 'Obavijesti', onPressed: () => context.push('/notifications'))
                : SnackBarAction(label: 'Otvori', onPressed: () => context.push('/appointments/$appointmentId')),
          ),
        );
    });

    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (index) => shell.goBranch(index, initialLocation: index == shell.currentIndex),
        destinations: [
          for (final (icon, selected, label) in _tabs)
            NavigationDestination(icon: Icon(icon), selectedIcon: Icon(selected), label: label),
        ],
      ),
    );
  }
}
