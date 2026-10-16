import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../appointments/appointment_card.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).value;
    final recent = ref.watch(recentServicesProvider);
    final money = ref.watch(moneyProvider);

    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: () => ref.refresh(recentServicesProvider.future),
          child: ListView(
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              _Hero(firstName: user?.firstName),
              const Padding(
                padding: EdgeInsets.fromLTRB(16, 8, 16, 12),
                child: Text('Naše usluge', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
              ),
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16),
                child: _Shortcuts(),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 24, 8, 8),
                child: Row(
                  children: [
                    const Expanded(
                      child: Text('Nedavni servisi', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
                    ),
                    TextButton.icon(
                      onPressed: () => context.go('/history'),
                      iconAlignment: IconAlignment.end,
                      icon: const Icon(Icons.arrow_forward, size: 18),
                      label: const Text('Vidi sve'),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: recent.when(
                  data: (items) => items.isEmpty
                      ? const Card(
                          child: Padding(
                            padding: EdgeInsets.all(20),
                            child: Text(
                              'Još nemate završenih servisa. Zakažite prvi termin.',
                              style: TextStyle(color: AppColors.textSecondary),
                            ),
                          ),
                        )
                      : Column(
                          children: [
                            for (final item in items)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 12),
                                child: AppointmentCard(item: item, money: money),
                              ),
                          ],
                        ),
                  loading: () => const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (error, _) => ErrorView(
                    message: 'Nedavne servise nije moguće učitati.',
                    onRetry: () => ref.invalidate(recentServicesProvider),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Hero extends ConsumerWidget {
  const _Hero({required this.firstName});

  final String? firstName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadCountProvider).value ?? 0;

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 8, 8, 28),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFF0B1324), AppColors.background],
        ),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  firstName == null ? 'Dobro došli' : 'Zdravo, $firstName',
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
              ),
              IconButton(
                tooltip: 'Obavijesti',
                onPressed: () => context.push('/notifications'),
                icon: Badge(
                  isLabelVisible: unread > 0,
                  label: Text('$unread'),
                  child: const Icon(Icons.notifications_outlined),
                ),
              ),
            ],
          ),
          const Icon(Icons.directions_car_filled_outlined, size: 72, color: Color(0xFF1E3A8A)),
          const Text('Smart Garage', style: TextStyle(fontSize: 36, fontWeight: FontWeight.w800)),
          const SizedBox(height: 6),
          const Text('Vaš partner za auto servis', style: TextStyle(color: AppColors.textSecondary, fontSize: 16)),
          const SizedBox(height: 20),
          FilledButton(
            style: FilledButton.styleFrom(
              minimumSize: const Size(190, 50),
              shape: const StadiumBorder(),
            ),
            onPressed: () => context.go('/book'),
            child: const Text('Zakažite servis'),
          ),
        ],
      ),
    );
  }
}

class _Shortcuts extends StatelessWidget {
  const _Shortcuts();

  @override
  Widget build(BuildContext context) {
    final cards = [
      (Icons.build_outlined, 'Servisne usluge', 'Pregledajte naše usluge', () => context.go('/services')),
      (Icons.schedule, 'Brzo zakazivanje', 'Zakažite termin u par klikova', () => context.go('/book')),
      (Icons.directions_car_outlined, 'Vaša vozila', 'Upravljajte vozilima', () => context.push('/vehicles')),
      (Icons.trending_up, 'Preporuke', 'Personalizirane preporuke', () => context.push('/recommendations')),
    ];

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.05,
      children: [
        for (final (icon, title, subtitle, onTap) in cards)
          Card(
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    IconTile(icon: icon),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                        const SizedBox(height: 4),
                        Text(subtitle, style: const TextStyle(color: AppColors.textSecondary, fontSize: 13)),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
