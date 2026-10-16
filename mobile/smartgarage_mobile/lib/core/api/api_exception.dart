import 'package:dio/dio.dart';

/// A failed API call, in the form the screens show it: the server's own message, and the
/// per-field messages that belong under the matching input.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.fieldErrors = const {}});

  /// Builds the exception from the API's standard error body
  /// (`{ status, title, detail, correlationId, errors }`).
  factory ApiException.fromDio(DioException error) {
    final response = error.response;

    if (response == null) {
      return ApiException(switch (error.type) {
        DioExceptionType.connectionTimeout ||
        DioExceptionType.sendTimeout ||
        DioExceptionType.receiveTimeout =>
          'Server ne odgovara. Provjerite internet vezu i pokušajte ponovo.',
        _ => 'Server nije dostupan. Provjerite internet vezu i pokušajte ponovo.',
      });
    }

    final body = response.data;
    final status = response.statusCode;

    if (body is Map<String, dynamic>) {
      final fieldErrors = <String, String>{};
      final errors = body['errors'];

      if (errors is Map<String, dynamic>) {
        for (final entry in errors.entries) {
          final messages = entry.value;
          if (messages is List && messages.isNotEmpty) {
            fieldErrors[entry.key] = messages.first.toString();
          }
        }
      }

      final detail = body['detail'] as String?;
      return ApiException(
        detail ?? body['title'] as String? ?? _fallback(status),
        statusCode: status,
        fieldErrors: fieldErrors,
      );
    }

    return ApiException(_fallback(status), statusCode: status);
  }

  final String message;
  final int? statusCode;

  /// The first server message per field, keyed by the camelCase field name.
  final Map<String, String> fieldErrors;

  bool get isUnauthorized => statusCode == 401;

  static String _fallback(int? status) => switch (status) {
        401 => 'Sesija je istekla. Prijavite se ponovo.',
        403 => 'Nemate dozvolu za ovu radnju.',
        404 => 'Traženi zapis nije pronađen.',
        _ => 'Došlo je do greške na serveru. Pokušajte ponovo.',
      };

  @override
  String toString() => message;
}

/// Turns anything a call can throw into the message a user should read.
String errorMessage(Object error) =>
    error is ApiException ? error.message : 'Došlo je do neočekivane greške. Pokušajte ponovo.';
