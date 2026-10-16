import 'package:flutter/material.dart';

import '../../core/formatting.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../data/models/review.dart';

/// Average rating with the count for each star, 5 down to 1.
class RatingSummaryCard extends StatelessWidget {
  const RatingSummaryCard({required this.summary, super.key});

  final ReviewSummary summary;

  @override
  Widget build(BuildContext context) {
    final average = summary.averageRating;
    final total = summary.totalCount;

    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    average == null ? '–' : average.toStringAsFixed(1),
                    style: const TextStyle(fontSize: 44, fontWeight: FontWeight.w800, height: 1),
                  ),
                  const SizedBox(height: 6),
                  Stars(rating: average ?? 0, size: 24),
                ],
              ),
              const Spacer(),
              Text('Ukupno recenzija: $total', style: const TextStyle(color: AppColors.textSecondary)),
            ],
          ),
          const SizedBox(height: 16),
          const Text('Distribucija ocjena:', style: TextStyle(color: AppColors.textSecondary, fontSize: 13)),
          const SizedBox(height: 8),
          for (var stars = 5; stars >= 1; stars--)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: Row(
                children: [
                  SizedBox(
                    width: 34,
                    child: Row(
                      children: [
                        Text('$stars', style: const TextStyle(fontSize: 13)),
                        const Icon(Icons.star_rounded, size: 14, color: AppColors.textSecondary),
                      ],
                    ),
                  ),
                  Expanded(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value: total == 0 ? 0 : summary.countFor(stars) / total,
                        minHeight: 8,
                        color: AppColors.warning,
                        backgroundColor: AppColors.surfaceRaised,
                      ),
                    ),
                  ),
                  SizedBox(
                    width: 36,
                    child: Text(
                      '${summary.countFor(stars)}',
                      textAlign: TextAlign.end,
                      style: const TextStyle(color: AppColors.textSecondary, fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class ReviewCard extends StatelessWidget {
  const ReviewCard({required this.review, this.onDelete, super.key});

  final Review review;

  /// Offered only on the customer's own reviews.
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Icon(Icons.account_circle, size: 20, color: AppColors.textSecondary),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(review.authorDisplayName, style: const TextStyle(fontWeight: FontWeight.w800)),
                  ),
                  if (onDelete != null)
                    IconButton(
                      tooltip: 'Obriši recenziju',
                      onPressed: onDelete,
                      icon: const Icon(Icons.delete_outline, color: AppColors.danger),
                    ),
                ],
              ),
              Stars(rating: review.rating.toDouble()),
              const SizedBox(height: 8),
              Pill(label: review.serviceName, color: AppColors.accent, icon: Icons.build_outlined),
              if (!review.isVisible) ...[
                const SizedBox(height: 8),
                const Pill(
                  label: 'Sakrio administrator - vidite je samo vi',
                  color: AppColors.warning,
                  icon: Icons.visibility_off_outlined,
                ),
              ],
              if (review.comment?.isNotEmpty ?? false) ...[
                const SizedBox(height: 10),
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: Text(review.comment!, style: const TextStyle(height: 1.4)),
                ),
              ],
              const SizedBox(height: 10),
              Row(
                children: [
                  const Icon(Icons.calendar_today_outlined, size: 13, color: AppColors.textSecondary),
                  const SizedBox(width: 6),
                  Text(
                    Fmt.date(review.createdAtUtc),
                    style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
                  ),
                ],
              ),
            ],
          ),
        ),
      );
}
