/// Build-time settings. The API address comes from
/// `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000`; the default is the Android
/// emulator's address for the host machine, so a plain `flutter build apk` runs as delivered.
abstract final class AppConfig {
  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000',
  );

  static const merchantDisplayName = 'SmartGarage';

  static const notificationHubPath = '/hubs/notifications';

  /// How often the unread badge is refreshed while the live connection is down.
  static const notificationPollInterval = Duration(seconds: 30);

  static const pageSize = 20;
}
