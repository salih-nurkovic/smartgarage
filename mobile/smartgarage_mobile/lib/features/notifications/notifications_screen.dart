import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_list.dart';
import '../../data/models/notification.dart';

/// The inbox. New notifications arrive live; read state and deletions go to the server.
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  Future<void> _act(BuildContext context, WidgetRef ref, Future<void> Function() action, [String? done]) async {
    try {
      await action();
      ref.read(notificationsVersionProvider.notifier).bump();
      if (done != null && context.mounted) {
        showMessage(context, done);
      }
    } on ApiException catch (error) {
      if (context.mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  Future<void> _open(BuildContext context, WidgetRef ref, AppNotification notification) async {
    final repository = ref.read(notificationRepositoryProvider);
    if (!notification.isRead) {
      await _act(context, ref, () => repository.markRead(notification.id));
    }
    if (notification.appointmentId case final id? when context.mounted) {
      await context.push('/appointments/$id');
    }
  }

  Future<void> _delete(BuildContext context, WidgetRef ref, AppNotification notification) async {
    final confirmed = await confirm(
      context,
      title: 'Obrisati obavijest?',
      message: '"${notification.title}" bit će uklonjena iz vaših obavijesti.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (confirmed && context.mounted) {
      await _act(context, ref, () => ref.read(notificationRepositoryProvider).delete(notification.id), 'Obavijest je obrisana.');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final repository = ref.watch(notificationRepositoryProvider);
    final unread = ref.watch(unreadCountProvider).value ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: Row(
          children: [
            const Text('Obavijesti'),
            if (unread > 0) ...[
              const SizedBox(width: 8),
              Badge(label: Text('$unread'), backgroundColor: AppColors.danger),
            ],
          ],
        ),
      ),
      body: PagedList<AppNotification>(
        reloadKey: ref.watch(notificationsVersionProvider),
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        fetch: (page) => repository.list(page: page),
        header: Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Align(
            alignment: Alignment.centerRight,
            child: OutlinedButton.icon(
              style: OutlinedButton.styleFrom(minimumSize: const Size(0, 40)),
              onPressed: unread == 0
                  ? null
                  : () => _act(context, ref, repository.markAllRead, 'Sve obavijesti su označene kao pročitane.'),
              icon: const Icon(Icons.done_all, size: 18),
              label: const Text('Označi sve pročitano'),
            ),
          ),
        ),
        empty: const EmptyView(icon: Icons.notifications_none, message: 'Nemate obavijesti.'),
        itemBuilder: (context, notification, _) => _NotificationCard(
          notification: notification,
          onOpen: () => _open(context, ref, notification),
          onMarkRead: () => _act(context, ref, () => repository.markRead(notification.id)),
          onDelete: () => _delete(context, ref, notification),
        ),
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({
    required this.notification,
    required this.onOpen,
    required this.onMarkRead,
    required this.onDelete,
  });

  final AppNotification notification;
  final VoidCallback onOpen;
  final VoidCallback onMarkRead;
  final VoidCallback onDelete;

  (IconData, Color, String) get _style => switch (notification.kind) {
        NotificationKind.appointment => (Icons.event_note, AppColors.accent, 'Rezervacija'),
        NotificationKind.reminder => (Icons.notifications_active_outlined, AppColors.urgent, 'Podsjetnik'),
        NotificationKind.payment => (Icons.credit_card, AppColors.success, 'Plaćanje'),
        NotificationKind.account => (Icons.manage_accounts_outlined, AppColors.textSecondary, 'Nalog'),
      };

  @override
  Widget build(BuildContext context) {
    final (icon, color, category) = _style;
    final unread = !notification.isRead;

    return Container(
      decoration: BoxDecoration(
        color: unread ? AppColors.surfaceRaised : AppColors.surface,
        borderRadius: BorderRadius.circular(AppTheme.radius),
        border: Border(left: BorderSide(color: unread ? AppColors.warning : AppColors.accent, width: 4)),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        onTap: onOpen,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 12, 8, 4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(icon, size: 18, color: color),
                  const SizedBox(width: 6),
                  Expanded(child: Text(category, style: const TextStyle(fontWeight: FontWeight.w800))),
                  Text(
                    Fmt.dateTime(notification.createdAtUtc),
                    style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                  ),
                  const SizedBox(width: 6),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                notification.title,
                style: TextStyle(fontWeight: unread ? FontWeight.w800 : FontWeight.w600),
              ),
              const SizedBox(height: 2),
              Text(notification.body, style: const TextStyle(color: AppColors.textSecondary, height: 1.35)),
              Row(
                children: [
                  if (unread)
                    TextButton.icon(
                      onPressed: onMarkRead,
                      icon: const Icon(Icons.check_circle_outline, size: 18),
                      label: const Text('Označi pročitano'),
                    ),
                  TextButton.icon(
                    style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                    onPressed: onDelete,
                    icon: const Icon(Icons.delete_outline, size: 18),
                    label: const Text('Obriši'),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
