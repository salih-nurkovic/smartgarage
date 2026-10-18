import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// The session tokens, kept in the platform keystore so they survive a restart, and in memory
/// so every request can read them without waiting on storage.
class TokenStorage {
  TokenStorage([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  static const _accessKey = 'access_token';
  static const _refreshKey = 'refresh_token';

  final FlutterSecureStorage _storage;

  String? _accessToken;
  String? _refreshToken;

  String? get accessToken => _accessToken;

  String? get refreshToken => _refreshToken;

  bool get hasSession => _refreshToken != null;

  Future<void> load() async {
    final values = await Future.wait([_storage.read(key: _accessKey), _storage.read(key: _refreshKey)]);
    _accessToken = values[0];
    _refreshToken = values[1];
  }

  Future<void> save(String accessToken, String refreshToken) async {
    _accessToken = accessToken;
    _refreshToken = refreshToken;
    await Future.wait([
      _storage.write(key: _accessKey, value: accessToken),
      _storage.write(key: _refreshKey, value: refreshToken),
    ]);
  }

  Future<void> clear() async {
    _accessToken = null;
    _refreshToken = null;
    await Future.wait([_storage.delete(key: _accessKey), _storage.delete(key: _refreshKey)]);
  }
}
