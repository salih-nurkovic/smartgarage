import '../../core/formatting.dart';
import 'common.dart';

/// Days as the API names them (System.DayOfWeek), Monday first as the schedule is shown.
enum WeekDay {
  monday('Ponedjeljak', 'Monday'),
  tuesday('Utorak', 'Tuesday'),
  wednesday('Srijeda', 'Wednesday'),
  thursday('Četvrtak', 'Thursday'),
  friday('Petak', 'Friday'),
  saturday('Subota', 'Saturday'),
  sunday('Nedjelja', 'Sunday');

  const WeekDay(this.label, this.apiName);

  final String label;
  final String apiName;

  static WeekDay parse(Object? value) => values.firstWhere((d) => d.apiName == value);
}

class ScheduleDay {
  const ScheduleDay({required this.day, required this.start, required this.end, required this.isWorkingDay});

  factory ScheduleDay.fromJson(Json json) => ScheduleDay(
    day: WeekDay.parse(json['dayOfWeek']),
    start: _time(json['startTime'] as String),
    end: _time(json['endTime'] as String),
    isWorkingDay: json['isWorkingDay'] as bool,
  );

  final WeekDay day;

  /// Minutes after midnight.
  final int start;
  final int end;
  final bool isWorkingDay;

  ScheduleDay copyWith({int? start, int? end, bool? isWorkingDay}) => ScheduleDay(
    day: day,
    start: start ?? this.start,
    end: end ?? this.end,
    isWorkingDay: isWorkingDay ?? this.isWorkingDay,
  );

  Json toJson() => {
    'dayOfWeek': day.apiName,
    'startTime': formatMinutes(start),
    'endTime': formatMinutes(end),
    'isWorkingDay': isWorkingDay,
  };

  static int _time(String value) {
    final [hours, minutes, ...] = value.split(':');
    return int.parse(hours) * 60 + int.parse(minutes);
  }

  static String formatMinutes(int minutes) =>
      '${(minutes ~/ 60).toString().padLeft(2, '0')}:${(minutes % 60).toString().padLeft(2, '0')}';
}

class Employee {
  const Employee({
    required this.id,
    required this.userId,
    required this.userName,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phoneNumber,
    required this.position,
    required this.specialization,
    required this.hireDateUtc,
    required this.isActive,
    required this.isAvailableForAssignment,
    required this.completedJobs,
    required this.averageRating,
    required this.schedule,
  });

  factory Employee.fromJson(Json json) => Employee(
    id: json['id'] as int,
    userId: json['userId'] as int,
    userName: json['userName'] as String,
    firstName: json['firstName'] as String,
    lastName: json['lastName'] as String,
    email: json['email'] as String,
    phoneNumber: json['phoneNumber'] as String,
    position: json['position'] as String,
    specialization: json['specialization'] as String?,
    hireDateUtc: parseUtc(json['hireDateUtc'] as String),
    isActive: json['isActive'] as bool,
    isAvailableForAssignment: json['isAvailableForAssignment'] as bool,
    completedJobs: json['completedJobs'] as int,
    averageRating: toDoubleOrNull(json['averageRating']),
    schedule: toList(json['schedule'], ScheduleDay.fromJson),
  );

  final int id;
  final int userId;
  final String userName;
  final String firstName;
  final String lastName;
  final String email;
  final String phoneNumber;
  final String position;
  final String? specialization;
  final DateTime hireDateUtc;
  final bool isActive;
  final bool isAvailableForAssignment;
  final int completedJobs;
  final double? averageRating;
  final List<ScheduleDay> schedule;

  String get fullName => '$firstName $lastName';

  String get initials => '${firstName.isEmpty ? '' : firstName[0]}${lastName.isEmpty ? '' : lastName[0]}';

  int yearsOfService(DateTime now) {
    var years = now.year - hireDateUtc.year;
    if (now.month < hireDateUtc.month || (now.month == hireDateUtc.month && now.day < hireDateUtc.day)) {
      years--;
    }
    return years < 0 ? 0 : years;
  }
}
