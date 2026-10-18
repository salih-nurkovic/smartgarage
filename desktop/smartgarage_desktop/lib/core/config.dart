/// Build-time settings. The API address comes from
/// `flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5000`; the default is the
/// API running on the same machine, so a plain `flutter build windows` runs as delivered.
abstract final class AppConfig {
  static const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5000');

  static const notificationHubPath = '/hubs/notifications';

  /// How often the unread badge is refreshed while the live connection is down.
  static const notificationPollInterval = Duration(seconds: 30);
}
