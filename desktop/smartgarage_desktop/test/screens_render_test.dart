import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:smartgarage_desktop/app/router.dart';
import 'package:smartgarage_desktop/core/providers.dart';
import 'package:smartgarage_desktop/core/realtime.dart';
import 'package:smartgarage_desktop/core/theme.dart';

import 'support/fake_api.dart';
import 'support/fonts.dart';

/// Every screen on a laptop-sized window, with data in the shape the API sends: each must build
/// and lay out without an exception or an overflow. Run as both roles, so the separation
/// between administrator and employee is checked too.
void main() {
  Future<GoRouter> start(WidgetTester tester, {bool admin = true}) async {
    tester.view
      ..physicalSize = const Size(1440, 900)
      ..devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    final container = ProviderContainer(
      overrides: [
        apiClientProvider.overrideWithValue(FakeApi()),
        authProvider.overrideWith(admin ? FakeAdministrator.new : FakeEmployee.new),
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

  for (final (path, expected) in [
    ('/dashboard', 'Prihodi po mjesecima'),
    ('/appointments', 'Upravljanje rezervacijama'),
    ('/appointments/2', 'Potvrdi'),
    ('/services', 'Upravljanje servisnim uslugama'),
    ('/employees', 'Upravljanje zaposlenicima'),
    ('/users', 'Korisnici'),
    ('/payments', 'Upravljanje plaćanjima'),
    ('/reviews', 'Haris M.'),
    ('/reports', 'Prihodi i rezervacije'),
    ('/reference', 'Šifarnici'),
    ('/error-log', 'Log grešaka'),
    ('/notifications', 'Nova rezervacija'),
    ('/profile', 'Promjena lozinke'),
  ]) {
    testWidgets('$path renders for an administrator', (tester) async {
      final router = await start(tester);
      router.go(path);
      await tester.pumpAndSettle();
      expect(find.textContaining(expected), findsWidgets);
    });
  }

  testWidgets('an employee sees neither administrator screens nor revenue', (tester) async {
    final router = await start(tester, admin: false);

    expect(find.text('Rezervacije'), findsWidgets);
    for (final hidden in ['Zaposlenici', 'Korisnici', 'Plaćanja', 'Izvještaji', 'Log grešaka']) {
      expect(find.text(hidden), findsNothing, reason: '$hidden is for administrators');
    }

    // A direct link to an administrator screen leads back to the dashboard.
    router.go('/payments');
    await tester.pumpAndSettle();
    expect(find.text('Upravljanje plaćanjima'), findsNothing);
    expect(find.text('Dashboard'), findsWidgets);
  });

  testWidgets('back leads from a detail to its list, and is disabled on a list', (tester) async {
    final router = await start(tester);
    router.go('/appointments/2');
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Nazad'));
    await tester.pumpAndSettle();
    expect(find.text('Upravljanje rezervacijama'), findsOneWidget);

    final back = tester.widget<IconButton>(
      find.ancestor(of: find.byIcon(Icons.arrow_back), matching: find.byType(IconButton)),
    );
    expect(back.onPressed, isNull);
  });

  testWidgets('a service that is on appointments cannot be deleted, and says why', (tester) async {
    final router = await start(tester);
    router.go('/services');
    await tester.pumpAndSettle();

    final deleteButtons = tester.widgetList<OutlinedButton>(
      find.ancestor(of: find.text('Obriši'), matching: find.byWidgetPredicate((w) => w is OutlinedButton)),
    );
    expect(deleteButtons.map((b) => b.onPressed == null), [true, false]);
    expect(
      find.byWidgetPredicate((w) => w is Tooltip && (w.message ?? '').contains('245 rezervacija')),
      findsOneWidget,
    );
  });

  testWidgets('reports show the figures, both tables and collapse small slices of the chart', (tester) async {
    final router = await start(tester);
    router.go('/reports');
    await tester.pumpAndSettle();

    expect(find.text('Stopa zadovoljstva'), findsOneWidget);
    expect(find.text('81%'), findsOneWidget, reason: '4,05 of 5');
    expect(find.text('Ostalo'), findsOneWidget, reason: 'six categories become four and the rest');
    expect(find.text('PAY-2026-000455'), findsOneWidget);

    await tester.tap(find.text('Rezervacije').last);
    await tester.pumpAndSettle();
    expect(find.text('Amina Kovačević'), findsOneWidget);
    expect(find.text('Klijent je otkazao termin zbog spriječenosti.'), findsOneWidget);
    expect(find.text('PAY-2026-000455'), findsNothing);
  });
}
