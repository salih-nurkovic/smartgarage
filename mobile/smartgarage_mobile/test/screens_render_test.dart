import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:smartgarage_mobile/app/router.dart';
import 'package:smartgarage_mobile/core/providers.dart';
import 'package:smartgarage_mobile/core/realtime.dart';
import 'package:smartgarage_mobile/core/theme.dart';

import 'support/fake_api.dart';
import 'support/fonts.dart';

/// Every screen, on a phone-sized display, with data in the shape the API sends: each must
/// build and lay out without an exception or an overflow.
void main() {
  Future<GoRouter> start(WidgetTester tester, {bool signedIn = true}) async {
    tester.view
      ..physicalSize = const Size(412, 915)
      ..devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    final container = ProviderContainer(
      overrides: [
        apiClientProvider.overrideWithValue(FakeApi()),
        authProvider.overrideWith(signedIn ? FakeSignedIn.new : FakeSignedOut.new),
        notificationRealtimeProvider.overrideWithValue(null),
      ],
      retry: (_, _) => null,
    );
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: Consumer(
          builder: (context, ref, _) => MaterialApp.router(
            theme: AppTheme.dark(),
            locale: const Locale('bs'),
            supportedLocales: const [Locale('bs')],
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            routerConfig: ref.watch(routerProvider),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    return container.read(routerProvider);
  }

  setUpAll(() async {
    await initializeDateFormatting('bs');
    await loadAppFonts();
  });

  testWidgets('a signed-out user lands on the login screen', (tester) async {
    final router = await start(tester, signedIn: false);
    router.go('/home');
    await tester.pumpAndSettle();
    expect(find.text('Prijavi se'), findsOneWidget);
  });

  for (final (path, expected) in [
    ('/home', 'Naše usluge'),
    ('/services', 'Servisne usluge'),
    ('/services/3', 'Zamjena zupčastog kaiša'),
    ('/history', 'Historija rezervacija'),
    ('/appointments/3', 'Lokacija i kontakt'),
    ('/appointments/1/reschedule', 'Izaberite datum'),
    ('/vehicles', 'Sljedeći servis: 2. nov 2026.'),
    ('/vehicles/new', 'Broj šasije (VIN)'),
    ('/recommendations?vehicleId=1', 'Content-Based'),
    ('/notifications', 'Rezervacija potvrđena'),
    ('/profile', 'Amar Begić'),
    ('/reviews/new?appointmentId=3', 'Vaše ime (javno)'),
    ('/reviews', 'Haris M.'),
    ('/payments', 'PAY-2026-000469'),
    ('/book', 'Korak 1 od 4'),
  ]) {
    testWidgets('$path renders', (tester) async {
      final router = await start(tester);
      router.go(path);
      await tester.pumpAndSettle();
      expect(find.textContaining(expected), findsWidgets);
    });
  }

  testWidgets('booking walks through all four steps to the summary', (tester) async {
    final router = await start(tester);
    router.go('/book?vehicleId=1&serviceId=1');
    await tester.pumpAndSettle();
    expect(find.text('Korak 2 od 4'), findsOneWidget);

    await tester.tap(find.text('Nastavi'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Dodirnite za izbor datuma'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('20'));
    await tester.tap(find.text('Izaberi'));
    await tester.pumpAndSettle();

    // A full slot cannot be chosen; a free one can.
    await tester.tap(find.text('11:00'));
    await tester.pumpAndSettle();
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Nastavi')).onPressed, isNull);

    await tester.tap(find.text('09:00'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Nastavi'));
    await tester.pumpAndSettle();

    expect(find.text('Korak 4 od 4'), findsOneWidget);
    expect(find.text('Mali servis'), findsOneWidget);
    expect(find.text('09:00'), findsOneWidget);
  });
}
