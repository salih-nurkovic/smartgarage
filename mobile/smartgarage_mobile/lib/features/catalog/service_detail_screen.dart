import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/auth_image.dart';
import '../../core/widgets/common.dart';
import '../../data/models/review.dart';
import '../../data/models/service.dart';
import '../reviews/review_widgets.dart';

typedef _ServiceDetail = (ServiceItem service, ReviewSummary summary, List<Review> reviews);

/// Opening this screen is what the server records as having viewed the service.
final _serviceDetailProvider = FutureProvider.autoDispose.family<_ServiceDetail, int>((ref, id) async {
  final catalog = ref.watch(catalogRepositoryProvider);
  final reviews = ref.watch(reviewRepositoryProvider);

  // The three calls are independent, so they run at the same time.
  final (service, summary, page) = await (
    catalog.service(id),
    reviews.summary(serviceId: id),
    reviews.list(serviceId: id),
  ).wait;
  return (service, summary, page.items);
});

class ServiceDetailScreen extends ConsumerWidget {
  const ServiceDetailScreen({required this.serviceId, super.key});

  final int serviceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(_serviceDetailProvider(serviceId));
    final money = ref.watch(moneyProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Detalji usluge')),
      body: AsyncBody<_ServiceDetail>(
        value: detail,
        onRetry: () => ref.invalidate(_serviceDetailProvider(serviceId)),
        builder: (data) {
          final (service, summary, reviews) = data;
          final interval = Fmt.every(km: service.mileageIntervalKm, months: service.monthsInterval);

          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
            children: [
              // Kept to a fixed height so the picture never takes over the screen.
              AuthImage(
                url: service.imageUrl,
                fallbackIcon: Icons.build_outlined,
                width: double.infinity,
                height: 180,
              ),
              const SizedBox(height: 16),
              Text(service.name, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800)),
              const SizedBox(height: 8),
              Align(alignment: Alignment.centerLeft, child: Pill(label: service.categoryName, color: AppColors.accent)),
              const SizedBox(height: 12),
              Text(service.description, style: const TextStyle(color: AppColors.textSecondary, height: 1.4)),
              const SizedBox(height: 16),
              SectionCard(
                child: Column(
                  children: [
                    KeyValueRow(label: 'Cijena', value: money(service.price)),
                    KeyValueRow(label: 'Trajanje', value: Fmt.duration(service.durationMinutes)),
                    if (interval.isNotEmpty) KeyValueRow(label: 'Preporučeni interval', value: interval),
                    KeyValueRow(
                      label: 'Za motore',
                      value: service.engineTypes.isEmpty
                          ? 'Svi tipovi motora'
                          : service.engineTypes.map((e) => e.name).join(', '),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: () => context.go('/book?serviceId=${service.id}'),
                icon: const Icon(Icons.calendar_month_outlined),
                label: const Text('Zakaži ovu uslugu'),
              ),
              const SizedBox(height: 24),
              const Text('Ocjene korisnika', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
              const SizedBox(height: 12),
              RatingSummaryCard(summary: summary),
              const SizedBox(height: 12),
              if (reviews.isEmpty)
                const Text('Ova usluga još nema recenzija.', style: TextStyle(color: AppColors.textSecondary)),
              for (final review in reviews.take(3))
                Padding(padding: const EdgeInsets.only(bottom: 12), child: ReviewCard(review: review)),
              if (summary.totalCount > 3)
                TextButton(
                  onPressed: () => context.push('/reviews?serviceId=${service.id}'),
                  child: Text('Prikaži sve recenzije (${summary.totalCount})'),
                ),
            ],
          );
        },
      ),
    );
  }
}
