import 'package:intl/intl.dart';

const appLocale = 'bs';

/// Reads a server timestamp as UTC whether or not it carries a zone suffix. The API stores
/// UTC, but values read back from the database come without the `Z`, and Dart would otherwise
/// take them as local time.
DateTime parseUtc(String value) {
  final hasZone = value.endsWith('Z') || RegExp(r'[+-]\d\d:\d\d$').hasMatch(value);
  return DateTime.parse(hasZone ? value : '${value}Z');
}

DateTime? parseUtcOrNull(Object? value) => value is String ? parseUtc(value) : null;

/// Dates and times as the garage keeps them. Working hours are defined in UTC and shown as
/// they are ("garage time"); converting to the phone's zone would move every slot.
abstract final class Fmt {
  static final _date = DateFormat('d. MMM yyyy.', appLocale);
  static final _dayMonth = DateFormat('EEEE, d. MMMM', appLocale);
  static final _time = DateFormat('HH:mm', appLocale);
  static final _dateTime = DateFormat('d. MMM yyyy. HH:mm', appLocale);
  static final _number = NumberFormat.decimalPattern(appLocale);

  static String date(DateTime value) => _date.format(value);

  static String dayMonth(DateTime value) => _dayMonth.format(value);

  static String time(DateTime value) => _time.format(value);

  static String dateTime(DateTime value) => _dateTime.format(value);

  static String km(int value) => '${_number.format(value)} km';

  static String duration(int minutes) {
    if (minutes < 60) {
      return '$minutes min';
    }
    final hours = minutes ~/ 60;
    final rest = minutes % 60;
    return rest == 0 ? '$hours h' : '$hours h $rest min';
  }

  /// The recommendation card's line, as in the mockup: `interval: 90.000 km | preporuka: 5 god.`
  static String interval({int? km, int? months}) => [
        if (km != null) 'interval: ${Fmt.km(km)}',
        if (months != null) 'preporuka: ${_period(months)}',
      ].join(' | ');

  /// The same interval as a phrase: `svakih 15.000 km ili 1 god.`
  static String every({int? km, int? months}) {
    final parts = [if (km != null) Fmt.km(km), if (months != null) _period(months)];
    return parts.isEmpty ? '' : 'svakih ${parts.join(' ili ')}';
  }

  static String _period(int months) => months % 12 == 0 ? '${months ~/ 12} god.' : '$months mj.';
}

/// Prices labelled with the currency the garage configures (`DISPLAY_CURRENCY`).
class MoneyFormat {
  MoneyFormat(String currencyCode)
      : _format = NumberFormat.currency(
          locale: appLocale,
          symbol: switch (currencyCode.toUpperCase()) {
            'EUR' => '€',
            'BAM' => 'KM',
            final other => other,
          },
          decimalDigits: 2,
        );

  final NumberFormat _format;

  String call(num amount) => _format.format(amount);
}
