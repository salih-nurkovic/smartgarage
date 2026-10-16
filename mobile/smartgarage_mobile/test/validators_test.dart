import 'package:flutter_test/flutter_test.dart';
import 'package:smartgarage_mobile/core/validators.dart';

void main() {
  group('password', () {
    test('accepts a password that meets every rule', () {
      expect(Validators.password('Sigurna#2026'), isNull);
    });

    test('names the first rule that is broken', () {
      expect(Validators.password(''), 'Lozinka je obavezno polje.');
      expect(Validators.password('Kratka1!'.substring(0, 6)), contains('najmanje 8'));
      expect(Validators.password('bezcifre!A'), contains('cifru'));
      expect(Validators.password('malaslova1!'), contains('veliko slovo'));
      expect(Validators.password('VELIKA1!AA'), contains('malo slovo'));
      expect(Validators.password('BezZnaka123'), contains('specijalni znak'));
    });
  });

  test('confirmation must match the password', () {
    expect(Validators.confirmPassword('abc', 'abc'), isNull);
    expect(Validators.confirmPassword('abd', 'abc'), 'Lozinke se ne podudaraju.');
  });

  test('e-mail and phone formats match the server rules', () {
    expect(Validators.email('amar@smartgarage.ba'), isNull);
    expect(Validators.email('amar@smartgarage'), contains('ime@domena.com'));
    expect(Validators.phone('+387 61 123 456'), isNull);
    expect(Validators.phone('061/123-456'), isNull);
    expect(Validators.phone('abc'), contains('+387 61 123 456'));
  });

  test('VIN has 17 characters and never I, O or Q', () {
    expect(Validators.vin('WVWZZZ1KZAW123456'), isNull);
    expect(Validators.vin('wvwzzz1kzaw123456'), isNull, reason: 'the form upper-cases it before sending');
    expect(Validators.vin('WVWZZZ1KZAW12345'), contains('17'));
    expect(Validators.vin('WVWZZZ1KZAO123456'), contains('bez slova I, O i Q'));
  });

  test('mileage can only go up when editing', () {
    expect(Validators.mileage('182400', minimum: 182400), isNull);
    expect(Validators.mileage('182000', minimum: 182400), contains('182400'));
    expect(Validators.mileage('-5'), isNotNull);
    expect(Validators.mileage('abc'), isNotNull);
  });

  test('production year is between 1950 and next year', () {
    final nextYear = DateTime.now().year + 1;
    expect(Validators.productionYear('1950'), isNull);
    expect(Validators.productionYear('$nextYear'), isNull);
    expect(Validators.productionYear('1949'), isNotNull);
    expect(Validators.productionYear('${nextYear + 1}'), isNotNull);
  });

  test('a cancellation reason needs 5 to 500 characters', () {
    expect(Validators.reason('Bolestan sam', 'Razlog'), isNull);
    expect(Validators.reason('Ne', 'Razlog'), contains('između 5 i 500'));
    expect(Validators.reason('   ', 'Razlog'), 'Razlog je obavezno polje.');
  });
}
