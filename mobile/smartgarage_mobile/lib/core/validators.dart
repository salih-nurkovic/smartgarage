/// Client-side checks with the same limits and wording as the API's validators, so a user sees
/// the problem under the field before sending. The server checks everything again; its answer
/// is shown under the same field when it disagrees.
abstract final class Validators {
  static const vinLength = 17;
  static const plateMinLength = 3;
  static const plateMaxLength = 15;
  static const vehicleYearMin = 1950;
  static const mileageMaxKm = 3000000;
  static const nameMinLength = 2;
  static const nameMaxLength = 50;
  static const passwordMinLength = 8;
  static const reasonMinLength = 5;
  static const reasonMaxLength = 500;
  static const notesMaxLength = 1000;
  static const reviewCommentMaxLength = 1000;

  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]{2,}$');
  static final _phone = RegExp(r'^\+?[0-9\s\-/()]{6,20}$');
  static final _userName = RegExp(r'^[a-zA-Z0-9._-]{3,50}$');
  static final _vin = RegExp(r'^[A-HJ-NPR-Z0-9]{17}$');
  static final _digits = RegExp(r'^\d+$');

  static String? required(String? value, String label) =>
      (value == null || value.trim().isEmpty) ? '$label je obavezno polje.' : null;

  static String? personName(String? value, String label) =>
      required(value, label) ??
      (value!.trim().length < nameMinLength || value.trim().length > nameMaxLength
          ? '$label: unesite između $nameMinLength i $nameMaxLength znakova.'
          : null);

  static String? userName(String? value) =>
      required(value, 'Korisničko ime') ??
      (_userName.hasMatch(value!.trim())
          ? null
          : 'Korisničko ime ima 3-50 znakova: slova, cifre, tačka, crtica ili donja crta.');

  static String? email(String? value) =>
      required(value, 'E-mail adresa') ??
      (_email.hasMatch(value!.trim()) ? null : 'Unesite ispravnu e-mail adresu u formatu ime@domena.com.');

  static String? phone(String? value) =>
      required(value, 'Broj telefona') ??
      (_phone.hasMatch(value!.trim()) ? null : 'Unesite ispravan broj telefona, npr. +387 61 123 456.');

  static String? password(String? value) {
    if (value == null || value.isEmpty) {
      return 'Lozinka je obavezno polje.';
    }
    if (value.length < passwordMinLength) {
      return 'Lozinka mora imati najmanje $passwordMinLength znakova.';
    }
    if (!value.contains(RegExp(r'[0-9]'))) {
      return 'Lozinka mora sadržavati najmanje jednu cifru (0-9).';
    }
    if (!value.contains(RegExp(r'[A-Z]'))) {
      return 'Lozinka mora sadržavati najmanje jedno veliko slovo (A-Z).';
    }
    if (!value.contains(RegExp(r'[a-z]'))) {
      return 'Lozinka mora sadržavati najmanje jedno malo slovo (a-z).';
    }
    if (!value.contains(RegExp(r'[^a-zA-Z0-9]'))) {
      return 'Lozinka mora sadržavati najmanje jedan specijalni znak, npr. ! ili #.';
    }
    return null;
  }

  static String? confirmPassword(String? value, String password) =>
      value != password ? 'Lozinke se ne podudaraju.' : null;

  static String? resetCode(String? value) =>
      required(value, 'Kod') ?? (_digits.hasMatch(value!.trim()) ? null : 'Kod se sastoji samo od cifara.');

  static String? plate(String? value) {
    final error = required(value, 'Registarska oznaka');
    if (error != null) {
      return error;
    }
    final length = value!.trim().length;
    return length < plateMinLength || length > plateMaxLength
        ? 'Registarska oznaka ima između $plateMinLength i $plateMaxLength znakova, npr. A12-K-345.'
        : null;
  }

  static String? vin(String? value) =>
      required(value, 'Broj šasije (VIN)') ??
      (_vin.hasMatch(value!.trim().toUpperCase())
          ? null
          : 'VIN ima tačno $vinLength znakova: velika slova i cifre, bez slova I, O i Q.');

  static String? productionYear(String? value) {
    final maxYear = DateTime.now().year + 1;
    final year = int.tryParse(value?.trim() ?? '');
    return year == null || year < vehicleYearMin || year > maxYear
        ? 'Godina proizvodnje mora biti između $vehicleYearMin i $maxYear.'
        : null;
  }

  static String? mileage(String? value, {int minimum = 0}) {
    final km = int.tryParse(value?.trim() ?? '');
    if (km == null || km < 0 || km > mileageMaxKm) {
      return 'Kilometraža mora biti cijeli broj između 0 i $mileageMaxKm km.';
    }
    return km < minimum ? 'Kilometraža ne može biti manja od trenutne ($minimum km).' : null;
  }

  static String? reason(String? value, String label) {
    final error = required(value, label);
    if (error != null) {
      return error;
    }
    final length = value!.trim().length;
    return length < reasonMinLength || length > reasonMaxLength
        ? '$label: unesite između $reasonMinLength i $reasonMaxLength znakova.'
        : null;
  }

  static String? maxLength(String? value, int max, String label) =>
      (value?.length ?? 0) > max ? '$label može imati najviše $max znakova.' : null;
}
