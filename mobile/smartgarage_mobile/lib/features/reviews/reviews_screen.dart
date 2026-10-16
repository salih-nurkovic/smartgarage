import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_list.dart';
import '../../data/models/review.dart';
import 'review_widgets.dart';

/// All reviews, or only the customer's own; optionally for one service.
class ReviewsScreen extends ConsumerStatefulWidget {
  const ReviewsScreen({this.serviceId, super.key});

  final int? serviceId;

  @override
  ConsumerState<ReviewsScreen> createState() => _ReviewsScreenState();
}

class _ReviewsScreenState extends ConsumerState<ReviewsScreen> {
  var _mine = false;
  var _version = 0;

  Future<void> _delete(Review review) async {
    final confirmed = await confirm(
      context,
      title: 'Obrisati recenziju?',
      message: 'Recenzija za uslugu "${review.serviceName}" bit će trajno obrisana.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!confirmed || !mounted) {
      return;
    }

    try {
      await ref.read(reviewRepositoryProvider).delete(review.id);
      if (mounted) {
        showMessage(context, 'Recenzija je obrisana.');
        setState(() => _version++);
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(reviewRepositoryProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Korisničke recenzije')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          await context.push('/reviews/new');
          if (mounted) {
            setState(() => _version++);
          }
        },
        icon: const Icon(Icons.rate_review_outlined),
        label: const Text('Ostavi recenziju'),
      ),
      body: PagedList<Review>(
        reloadKey: (_mine, _version),
        fetch: (page) => repository.list(serviceId: widget.serviceId, mine: _mine, page: page),
        header: Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: false, label: Text('Sve recenzije'), icon: Icon(Icons.forum_outlined)),
              ButtonSegment(value: true, label: Text('Moje recenzije'), icon: Icon(Icons.person_outline)),
            ],
            selected: {_mine},
            onSelectionChanged: (selection) => setState(() => _mine = selection.first),
          ),
        ),
        empty: EmptyView(
          icon: Icons.reviews_outlined,
          message: _mine ? 'Još niste ostavili nijednu recenziju.' : 'Još nema recenzija.',
        ),
        itemBuilder: (context, review, _) => ReviewCard(
          review: review,
          onDelete: review.isMine ? () => _delete(review) : null,
        ),
      ),
    );
  }
}
