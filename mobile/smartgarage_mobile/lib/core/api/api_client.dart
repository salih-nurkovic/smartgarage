import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../config.dart';
import 'api_exception.dart';
import 'token_storage.dart';

enum _RefreshOutcome { renewed, rejected, unreachable }

/// The one HTTP client of the app.
///
/// Every request carries the access token. When the server answers 401 the client renews the
/// session once with the refresh token and repeats the request; if the server refuses the
/// refresh, the session is over and [onSessionExpired] sends the user back to the login screen.
/// Expired tokens are never ignored.
class ApiClient {
  ApiClient(this._tokens)
      : _dio = Dio(_options()),
        _plain = Dio(_options()) {
    _dio.interceptors.add(
      QueuedInterceptorsWrapper(onRequest: _attachToken, onError: _renewOnUnauthorized),
    );
  }

  final TokenStorage _tokens;

  /// Requests from the screens; renews the session on 401.
  final Dio _dio;

  /// Refresh calls and repeated requests. It has no interceptors, so a request repeated from
  /// inside the error handler cannot queue behind that same handler.
  final Dio _plain;

  void Function()? onSessionExpired;

  String? get accessToken => _tokens.accessToken;

  static BaseOptions _options() => BaseOptions(
        baseUrl: AppConfig.apiBaseUrl,
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 30),
        listFormat: ListFormat.multi,
      );

  Future<T> get<T>(String path, T Function(dynamic json) parse, {Map<String, dynamic>? query}) =>
      _send(() => _dio.get<dynamic>(path, queryParameters: _clean(query)), parse);

  Future<T> post<T>(String path, T Function(dynamic json) parse, {Object? body}) =>
      _send(() => _dio.post<dynamic>(path, data: body ?? const <String, dynamic>{}), parse);

  Future<T> put<T>(String path, T Function(dynamic json) parse, {Object? body}) =>
      _send(() => _dio.put<dynamic>(path, data: body ?? const <String, dynamic>{}), parse);

  Future<T> patch<T>(String path, T Function(dynamic json) parse, {Object? body}) =>
      _send(() => _dio.patch<dynamic>(path, data: body ?? const <String, dynamic>{}), parse);

  Future<void> delete(String path) => _send(() => _dio.delete<dynamic>(path), (_) {});

  Future<Uint8List> bytes(String path) => _send(
        () => _dio.get<List<int>>(path, options: Options(responseType: ResponseType.bytes)),
        (data) => Uint8List.fromList(data as List<int>),
      );

  /// The anonymous calls (login, register, password reset) go out without a token and never
  /// trigger a refresh.
  Future<T> anonymous<T>(String path, T Function(dynamic json) parse, {required Object body}) =>
      _send(() => _plain.post<dynamic>(path, data: body), parse);

  Future<T> _send<T>(Future<Response<dynamic>> Function() request, T Function(dynamic json) parse) async {
    try {
      final response = await request();
      return parse(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  static Map<String, dynamic>? _clean(Map<String, dynamic>? query) =>
      query == null ? null : (Map.of(query)..removeWhere((_, value) => value == null || value == ''));

  void _attachToken(RequestOptions options, RequestInterceptorHandler handler) {
    final token = _tokens.accessToken;
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  Future<void> _renewOnUnauthorized(DioException error, ErrorInterceptorHandler handler) async {
    final request = error.requestOptions;

    if (error.response?.statusCode != 401 || request.extra['retried'] == true || !_tokens.hasSession) {
      return handler.next(error);
    }

    // Requests queue here while one refresh runs. One sent with a token that has since been
    // replaced only needs repeating; refreshing again would present a used refresh token, which
    // the server treats as theft and answers by ending the whole session.
    final sentWith = request.headers['Authorization'];
    final outcome = sentWith != 'Bearer ${_tokens.accessToken}' ? _RefreshOutcome.renewed : await _refresh();

    switch (outcome) {
      case _RefreshOutcome.renewed:
        request
          ..headers['Authorization'] = 'Bearer ${_tokens.accessToken}'
          ..extra['retried'] = true;
        try {
          return handler.resolve(await _plain.fetch<dynamic>(request));
        } on DioException catch (retryError) {
          return handler.next(retryError);
        }
      case _RefreshOutcome.rejected:
        await _tokens.clear();
        onSessionExpired?.call();
        return handler.next(error);
      case _RefreshOutcome.unreachable:
        return handler.next(error);
    }
  }

  Future<_RefreshOutcome> _refresh() async {
    try {
      final response = await _plain.post<Map<String, dynamic>>(
        '/api/auth/refresh',
        data: {'refreshToken': _tokens.refreshToken},
      );
      final data = response.data!;
      await _tokens.save(data['accessToken'] as String, data['refreshToken'] as String);
      return _RefreshOutcome.renewed;
    } on DioException catch (error) {
      // Only a refusal ends the session. A server error says nothing about the token, and
      // signing the user out because the database was slow would lose a valid session.
      final status = error.response?.statusCode;
      return status != null && status >= 400 && status < 500 ? _RefreshOutcome.rejected : _RefreshOutcome.unreachable;
    }
  }
}
