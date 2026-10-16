import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api/api_exception.dart';
import '../core/providers.dart';
import '../core/theme.dart';
import '../core/widgets/common.dart';

/// Shown while the saved session is restored, or when the server could not be reached to
/// restore it.
class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authProvider);

    return Scaffold(
      body: auth.hasError
          ? ErrorView(message: errorMessage(auth.error!), onRetry: () => ref.invalidate(authProvider))
          : const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.directions_car_filled_outlined, size: 72, color: AppColors.accent),
                  SizedBox(height: 12),
                  Text('Smart Garage', style: TextStyle(fontSize: 28, fontWeight: FontWeight.w800)),
                  SizedBox(height: 24),
                  CircularProgressIndicator(),
                ],
              ),
            ),
    );
  }
}
