import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartgarage_mobile/core/theme.dart';
import 'package:smartgarage_mobile/features/auth/login_screen.dart';

void main() {
  testWidgets('empty login shows a message under each field and sends nothing', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(theme: AppTheme.dark(), home: const LoginScreen()),
      ),
    );

    await tester.tap(find.text('Prijavi se'));
    await tester.pump();

    expect(find.text('Korisničko ime je obavezno polje.'), findsOneWidget);
    expect(find.text('Lozinka je obavezno polje.'), findsOneWidget);
  });
}
