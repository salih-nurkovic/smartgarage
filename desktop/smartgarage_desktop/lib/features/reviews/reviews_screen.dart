import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/admin.dart';

/// Moderation: an administrator hides a review instead of deleting it, so its author still sees
/// it and the decision can be reversed.
class ReviewsScreen extends ConsumerStatefulWidget {
  const ReviewsScreen({super.key});

  @override
  ConsumerState<ReviewsScreen> createState() => _ReviewsScreenState();
}

class _ReviewsScreenState extends ConsumerState<ReviewsScreen> {
  int? _rating;
  var _version = 0;

  Future<void> _toggle(Review review) async {
    final hide = review.isVisible;
    final ok = await confirm(
      context,
      title: hide ? 'Sakriti recenziju?' : 'Ponovo prikazati recenziju?',
      message: hide
          ? 'Recenzija korisnika ${review.authorDisplayName} više neće biti javna i neće ulaziti u prosjek ocjena. Autor je i dalje vidi.'
          : 'Recenzija korisnika ${review.authorDisplayName} ponovo će biti javna.',
      confirmLabel: hide ? 'Sakrij' : 'Prikaži',
      destructive: hide,
    );
    if (!ok || !mounted) {
      return;
    }
    try {
      await ref.read(reviewRepositoryProvider).setVisible(review.id, visible: !hide);
      if (mounted) {
        showMessage(context, hide ? 'Recenzija je sakrivena.' : 'Recenzija je ponovo vidljiva.');
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

    return ScreenScaffold(
      title: 'Recenzije',
      subtitle: 'Moderacija ocjena i komentara kupaca',
      actions: [
        SizedBox(
          width: 200,
          child: DropdownButtonFormField<int?>(
            initialValue: _rating,
            decoration: const InputDecoration(labelText: 'Ocjena'),
            items: [
              const DropdownMenuItem(child: Text('Sve ocjene')),
              for (var stars = 5; stars >= 1; stars--) DropdownMenuItem(value: stars, child: Text('$stars ★')),
            ],
            onChanged: (v) => setState(() => _rating = v),
          ),
        ),
      ],
      body: PagedView<Review>(
        reloadKey: (_rating, _version),
        fetch: (page) => repository.list(rating: _rating, page: page),
        emptyMessage: 'Nema recenzija.',
        builder: (context, items, _) => SimpleTable<Review>(
          items: items,
          columns: [
            TableColumn('Autor', (r) => Text(r.authorDisplayName, style: const TextStyle(fontWeight: FontWeight.w700))),
            TableColumn('Usluga', (r) => Text(r.serviceName)),
            TableColumn('Ocjena', (r) => Stars(rating: r.rating.toDouble())),
            TableColumn(
              'Komentar',
              (r) => Text(r.comment ?? '—', maxLines: 3, overflow: TextOverflow.ellipsis),
              flex: 3,
            ),
            TableColumn('Datum', (r) => Text(Fmt.date(r.createdAtUtc))),
            TableColumn(
              'Vidljivost',
              (r) => Row(
                children: [
                  Pill(
                    label: r.isVisible ? 'Javna' : 'Sakrivena',
                    color: r.isVisible ? AppColors.success : AppColors.warning,
                  ),
                  const Spacer(),
                  IconButton(
                    tooltip: r.isVisible ? 'Sakrij' : 'Prikaži',
                    onPressed: () => _toggle(r),
                    icon: Icon(r.isVisible ? Icons.visibility_off_outlined : Icons.visibility_outlined),
                  ),
                ],
              ),
              flex: 2,
            ),
          ],
        ),
      ),
    );
  }
}
