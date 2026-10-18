import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/notification.dart';

/// The staff member's inbox. New notifications arrive live.
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

  Future<void> _delete(BuildContext context, WidgetRef ref, AppNotification n) async {
    final ok = await confirm(
      context,
      title: 'Obrisati obavijest?',
      message: '„${n.title}" bit će uklonjena.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (ok && context.mounted) {
      await _act(context, ref, () => ref.read(notificationRepositoryProvider).delete(n.id), 'Obavijest je obrisana.');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final repository = ref.watch(notificationRepositoryProvider);
    final unread = ref.watch(unreadCountProvider).value ?? 0;

    return ScreenScaffold(
      title: 'Obavijesti',
      subtitle: unread == 0 ? 'Sve obavijesti su pročitane' : 'Nepročitanih: $unread',
      actions: [
        GuardedButton(
          label: 'Označi sve pročitano',
          icon: Icons.done_all,
          onPressed: unread == 0
              ? null
              : () => _act(context, ref, repository.markAllRead, 'Sve obavijesti su označene kao pročitane.'),
          disabledReason: 'Nema nepročitanih obavijesti.',
        ),
      ],
      body: PagedView<AppNotification>(
        reloadKey: ref.watch(notificationsVersionProvider),
        fetch: (page) => repository.list(page: page),
        emptyMessage: 'Nemate obavijesti.',
        builder: (context, items, _) => Column(
          children: [
            for (final n in items)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Container(
                  decoration: BoxDecoration(
                    color: n.isRead ? AppColors.surface : AppColors.surfaceRaised,
                    borderRadius: BorderRadius.circular(AppTheme.radius),
                    border: Border(left: BorderSide(color: n.isRead ? AppColors.accent : AppColors.warning, width: 4)),
                  ),
                  // Its own Material, so the hover and click feedback shows over the card colour.
                  child: Material(
                    type: MaterialType.transparency,
                    child: ListTile(
                      contentPadding: const EdgeInsets.fromLTRB(16, 8, 8, 8),
                      onTap: () async {
                        if (!n.isRead) {
                          await _act(context, ref, () => repository.markRead(n.id));
                        }
                        if (n.appointmentId case final id? when context.mounted) {
                          await context.push('/appointments/$id');
                        }
                      },
                      title: Text(n.title, style: TextStyle(fontWeight: n.isRead ? FontWeight.w500 : FontWeight.w800)),
                      subtitle: Text('${n.body}\n${Fmt.dateTime(n.createdAtUtc)}'),
                      isThreeLine: true,
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (!n.isRead)
                            IconButton(
                              tooltip: 'Označi pročitano',
                              onPressed: () => _act(context, ref, () => repository.markRead(n.id)),
                              icon: const Icon(Icons.check_circle_outline),
                            ),
                          IconButton(
                            tooltip: 'Obriši',
                            onPressed: () => _delete(context, ref, n),
                            icon: const Icon(Icons.delete_outline, color: AppColors.danger),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
