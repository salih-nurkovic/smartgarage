import '../../core/formatting.dart';
import 'common.dart';

abstract final class Roles {
  static const customer = 'Customer';
}

class User {
  const User({
    required this.userName,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.phoneNumber,
    required this.hasProfileImage,
    required this.roles,
    required this.createdAtUtc,
  });

  factory User.fromJson(Json json) => User(
        userName: json['userName'] as String,
        email: json['email'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        phoneNumber: json['phoneNumber'] as String,
        hasProfileImage: json['hasProfileImage'] as bool,
        roles: toStrings(json['roles']),
        createdAtUtc: parseUtc(json['createdAtUtc'] as String),
      );

  final String userName;
  final String email;
  final String firstName;
  final String lastName;
  final String phoneNumber;
  final bool hasProfileImage;
  final List<String> roles;
  final DateTime createdAtUtc;

  String get fullName => '$firstName $lastName';

  String get initials => '${firstName.isEmpty ? '' : firstName[0]}${lastName.isEmpty ? '' : lastName[0]}';

  bool get isCustomer => roles.contains(Roles.customer);
}

class AuthResult {
  const AuthResult({required this.accessToken, required this.refreshToken, required this.user});

  factory AuthResult.fromJson(dynamic json) {
    final map = json as Json;
    return AuthResult(
      accessToken: map['accessToken'] as String,
      refreshToken: map['refreshToken'] as String,
      user: User.fromJson(map['user'] as Json),
    );
  }

  final String accessToken;
  final String refreshToken;
  final User user;
}
