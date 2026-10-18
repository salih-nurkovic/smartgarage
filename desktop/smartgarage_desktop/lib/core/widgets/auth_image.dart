import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';
import '../theme.dart';

/// Image bytes by URL. Images are served only to signed-in users, so they go through the API
/// client (with its token and refresh) rather than Image.network.
final imageBytesProvider = FutureProvider.family<Uint8List, String>((ref, url) {
  ref.watch(sessionUserProvider);
  return ref.watch(dashboardRepositoryProvider).image(url);
});

class AuthImage extends ConsumerWidget {
  const AuthImage({required this.url, required this.fallbackIcon, this.width = 56, this.height = 56, super.key});

  final String? url;
  final IconData fallbackIcon;
  final double width;
  final double height;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fallback = Container(
      width: width,
      height: height,
      decoration: BoxDecoration(color: AppColors.accentSoft, borderRadius: BorderRadius.circular(10)),
      child: Icon(fallbackIcon, color: AppColors.accent),
    );

    final source = url;
    if (source == null) {
      return fallback;
    }

    return ref
        .watch(imageBytesProvider(source))
        .maybeWhen(
          data: (bytes) => ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: Image.memory(bytes, width: width, height: height, fit: BoxFit.cover, gaplessPlayback: true),
          ),
          orElse: () => fallback,
        );
  }
}
