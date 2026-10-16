import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../data/models/user.dart';

/// The profile picture, reloaded whenever the user changes (including a new upload).
final _profileImageProvider = FutureProvider.autoDispose.family<Uint8List, User>(
  (ref, user) => ref.watch(profileRepositoryProvider).image(),
);

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  static const _maxImageSide = 1024.0;

  var _uploading = false;

  Future<void> _changePhoto() async {
    final file = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      maxWidth: _maxImageSide,
      maxHeight: _maxImageSide,
      imageQuality: 85,
    );
    if (file == null || !mounted) {
      return;
    }

    setState(() => _uploading = true);
    try {
      final user = await ref.read(profileRepositoryProvider).uploadImage(await file.readAsBytes(), file.name);
      ref.read(authProvider.notifier).updateUser(user);
      if (mounted) {
        showMessage(context, 'Profilna slika je promijenjena.');
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    } finally {
      if (mounted) {
        setState(() => _uploading = false);
      }
    }
  }

  Future<void> _logout() async {
    final confirmed = await confirm(
      context,
      title: 'Odjaviti se?',
      message: 'Sesija će biti završena i na serveru. Za nastavak ćete se morati ponovo prijaviti.',
      confirmLabel: 'Odjavi se',
    );
    if (confirmed) {
      await ref.read(authProvider.notifier).logout();
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(authProvider).value;
    if (user == null) {
      return const SizedBox.shrink();
    }

    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
            const PageHeader(title: 'Profil'),
            Center(child: _Avatar(user: user, uploading: _uploading, onChange: _changePhoto)),
            const SizedBox(height: 12),
            Text(
              user.fullName,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
            ),
            Text('@${user.userName}', textAlign: TextAlign.center, style: const TextStyle(color: AppColors.textSecondary)),
            const SizedBox(height: 20),
            SectionCard(
              title: 'Lični podaci',
              child: Column(
                children: [
                  KeyValueRow(label: 'E-mail', value: user.email),
                  KeyValueRow(label: 'Telefon', value: user.phoneNumber),
                  KeyValueRow(label: 'Član od', value: Fmt.date(user.createdAtUtc)),
                ],
              ),
            ),
            const SizedBox(height: 16),
            Card(
              clipBehavior: Clip.antiAlias,
              child: Column(
                children: [
                  _tile(Icons.edit_outlined, 'Uredi profil', () => context.push('/profile/edit')),
                  _tile(Icons.lock_reset, 'Promijeni lozinku', () => context.push('/profile/password')),
                  _tile(Icons.directions_car_outlined, 'Moja vozila', () => context.push('/vehicles')),
                  _tile(Icons.trending_up, 'Preporuke', () => context.push('/recommendations')),
                  _tile(Icons.receipt_long_outlined, 'Moja plaćanja', () => context.push('/payments')),
                  _tile(Icons.reviews_outlined, 'Recenzije', () => context.push('/reviews')),
                  _tile(Icons.notifications_outlined, 'Obavijesti', () => context.push('/notifications')),
                ],
              ),
            ),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger),
              onPressed: _logout,
              icon: const Icon(Icons.logout),
              label: const Text('Odjavi se'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _tile(IconData icon, String label, VoidCallback onTap) => ListTile(
        leading: Icon(icon, color: AppColors.accent),
        title: Text(label),
        trailing: const Icon(Icons.chevron_right),
        onTap: onTap,
      );
}

class _Avatar extends ConsumerWidget {
  const _Avatar({required this.user, required this.uploading, required this.onChange});

  final User user;
  final bool uploading;
  final VoidCallback onChange;

  static const _size = 104.0;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final initials = CircleAvatar(
      radius: _size / 2,
      backgroundColor: AppColors.accentSoft,
      child: Text(user.initials, style: const TextStyle(fontSize: 34, fontWeight: FontWeight.w800, color: AppColors.accent)),
    );

    final picture = user.hasProfileImage
        ? ref.watch(_profileImageProvider(user)).maybeWhen(
              data: (bytes) => CircleAvatar(radius: _size / 2, backgroundImage: MemoryImage(bytes)),
              orElse: () => initials,
            )
        : initials;

    return Stack(
      children: [
        picture,
        Positioned(
          right: 0,
          bottom: 0,
          child: IconButton.filled(
            tooltip: 'Promijeni profilnu sliku',
            onPressed: uploading ? null : onChange,
            icon: uploading
                ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.photo_camera_outlined, size: 20),
          ),
        ),
      ],
    );
  }
}
