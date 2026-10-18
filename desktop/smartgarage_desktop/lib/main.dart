import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app/router.dart';
import 'core/formatting.dart';
import 'core/theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting(appLocale);

  runApp(
    // A failed request is shown with a retry button; Riverpod's automatic retry would repeat
    // refused or invalid requests in the background instead.
    ProviderScope(retry: (_, _) => null, child: const SmartGarageDesktop()),
  );
}

class SmartGarageDesktop extends ConsumerWidget {
  const SmartGarageDesktop({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => MaterialApp.router(
    title: 'Smart Garage — administracija',
    debugShowCheckedModeBanner: false,
    theme: AppTheme.dark(),
    locale: const Locale(appLocale),
    supportedLocales: const [Locale(appLocale)],
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    routerConfig: ref.watch(routerProvider),
  );
}
