import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../data/models/common.dart';
import '../data/models/notification.dart';
import 'api/api_client.dart';
import 'config.dart';
import 'providers.dart';

/// Keeps the notification inbox current without the user refreshing anything.
///
/// The API pushes each new notification over SignalR. While that connection is down - no
/// network, or the access token expired and the reconnect was refused - the unread count is
/// polled instead, and each poll also tries to bring the live connection back.
class NotificationRealtime {
  NotificationRealtime(this._api, {required this.onNotification, required this.onPoll});

  final ApiClient _api;
  final void Function(AppNotification notification) onNotification;
  final VoidCallback onPoll;

  HubConnection? _hub;
  Timer? _poll;
  bool _stopped = false;

  Future<void> start() async {
    _hub =
        HubConnectionBuilder()
            .withUrl(
              '${AppConfig.apiBaseUrl}${AppConfig.notificationHubPath}',
              options: HttpConnectionOptions(accessTokenFactory: () async => _api.accessToken ?? ''),
            )
            .withAutomaticReconnect()
            .build()
          ..on('NotificationReceived', _received);

    _poll = Timer.periodic(AppConfig.notificationPollInterval, (_) => _tick());
    await _connect();
  }

  Future<void> stop() async {
    _stopped = true;
    _poll?.cancel();
    await _hub?.stop();
  }

  bool get _connected => _hub?.state == HubConnectionState.Connected;

  Future<void> _tick() async {
    if (_connected || _stopped) {
      return;
    }
    // The poll goes through the API client, which renews an expired token; the hub can then
    // connect with the fresh one.
    onPoll();
    await _connect();
  }

  Future<void> _connect() async {
    if (_stopped || _hub?.state != HubConnectionState.Disconnected) {
      return;
    }
    try {
      await _hub!.start();
    } on Object catch (error) {
      debugPrint('Notifikacije uživo nisu dostupne, koristi se periodično osvježavanje: $error');
    }
  }

  void _received(List<Object?>? arguments) {
    final payload = arguments?.firstOrNull;
    if (payload is Json) {
      onNotification(AppNotification.fromJson(payload));
    }
  }
}

/// Shown by the app shell as a banner when a notification arrives.
final incomingNotificationProvider = NotifierProvider<IncomingNotification, AppNotification?>(IncomingNotification.new);

class IncomingNotification extends Notifier<AppNotification?> {
  @override
  AppNotification? build() => null;

  void show(AppNotification notification) => state = notification;
}

/// Alive while a customer is signed in; the app shell watches it.
final notificationRealtimeProvider = Provider<NotificationRealtime?>((ref) {
  if (ref.watch(sessionUserProvider) == null) {
    return null;
  }

  final realtime = NotificationRealtime(
    ref.watch(apiClientProvider),
    onNotification: (notification) {
      ref.read(notificationsVersionProvider.notifier).bump();
      if (notification.appointmentId != null) {
        ref.read(appointmentsVersionProvider.notifier).bump();
      }
      ref.read(incomingNotificationProvider.notifier).show(notification);
    },
    // Without the push nothing says what changed, so the inbox and the appointments are all
    // reloaded; screens keep showing their data while they refresh.
    onPoll: () {
      ref.read(notificationsVersionProvider.notifier).bump();
      ref.read(appointmentsVersionProvider.notifier).bump();
    },
  );

  unawaited(realtime.start());
  ref.onDispose(() => unawaited(realtime.stop()));
  return realtime;
});
