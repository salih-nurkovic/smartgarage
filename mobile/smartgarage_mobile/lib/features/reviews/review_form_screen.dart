import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/validators.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/form_state.dart';
import '../../data/models/review.dart';
import 'review_widgets.dart';

typedef _ReviewFormData = (ReviewSummary summary, List<ReviewableAppointment> reviewable);

final _reviewFormProvider = FutureProvider.autoDispose<_ReviewFormData>((ref) {
  final reviews = ref.watch(reviewRepositoryProvider);
  return (reviews.summary(), reviews.reviewable()).wait;
});

/// A review is left for a service on a completed appointment, once per appointment.
class ReviewFormScreen extends ConsumerStatefulWidget {
  const ReviewFormScreen({this.appointmentId, super.key});

  /// Preselects this appointment when the form is opened from its detail.
  final int? appointmentId;

  @override
  ConsumerState<ReviewFormScreen> createState() => _ReviewFormScreenState();
}

class _ReviewFormScreenState extends ConsumerState<ReviewFormScreen> with ServerFormState {
  final _comment = TextEditingController();
  (int appointmentId, int serviceId)? _choice;
  var _rating = 0;
  var _ratingTouched = false;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _publish() async {
    setState(() => _ratingTouched = true);
    final fieldsValid = formKey.currentState!.validate();
    if (!fieldsValid || _rating == 0) {
      return;
    }

    final (appointmentId, serviceId) = _choice!;
    final saved = await submit(
      () => ref.read(reviewRepositoryProvider).create(
            appointmentId: appointmentId,
            serviceId: serviceId,
            rating: _rating,
            comment: _comment.text.trim().isEmpty ? null : _comment.text.trim(),
          ),
    );

    if (saved && mounted) {
      ref.read(appointmentsVersionProvider.notifier).bump();
      showMessage(context, 'Hvala! Vaša recenzija je objavljena.');
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = ref.watch(_reviewFormProvider);
    final user = ref.watch(authProvider).value;
    final publicName = user == null ? '' : '${user.firstName} ${user.lastName.isEmpty ? '' : '${user.lastName[0]}.'}';

    return Scaffold(
      appBar: AppBar(title: const Text('Nova recenzija')),
      body: AsyncBody<_ReviewFormData>(
        value: data,
        onRetry: () => ref.invalidate(_reviewFormProvider),
        builder: (data) {
          final (summary, reviewable) = data;
          final options = [
            for (final appointment in reviewable)
              for (final service in appointment.services)
                ((appointment.appointmentId, service.id), '${service.name} (${Fmt.date(appointment.completedAtUtc)})'),
          ];
          _choice ??= options
              .where((o) => o.$1.$1 == widget.appointmentId)
              .map((o) => o.$1)
              .firstOrNull;

          return Form(
            key: formKey,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                RatingSummaryCard(summary: summary),
                const SizedBox(height: 16),
                if (options.isEmpty)
                  const SectionCard(
                    child: EmptyView(
                      icon: Icons.rate_review_outlined,
                      message: 'Recenziju možete ostaviti nakon završenog servisa. '
                          'Trenutno nemate završenih servisa bez recenzije.',
                    ),
                  )
                else
                  SectionCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        TextFormField(
                          initialValue: publicName,
                          readOnly: true,
                          decoration: const InputDecoration(
                            labelText: 'Vaše ime (javno)',
                            helperText: 'Ovako će vaše ime biti prikazano uz recenziju.',
                          ),
                        ),
                        const SizedBox(height: 16),
                        DropdownButtonFormField<(int, int)>(
                          initialValue: _choice,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'Odaberite uslugu'),
                          items: [
                            for (final (value, label) in options)
                              DropdownMenuItem(value: value, child: Text(label, overflow: TextOverflow.ellipsis)),
                          ],
                          onChanged: (v) {
                            clearServerError('serviceId');
                            clearServerError('appointmentId');
                            setState(() => _choice = v);
                          },
                          validator: (v) => v == null ? 'Odaberite uslugu koju ocjenjujete.' : null,
                          forceErrorText: serverError('serviceId') ?? serverError('appointmentId'),
                        ),
                        const SizedBox(height: 16),
                        const Text('Ocjena (1-5)', style: TextStyle(color: AppColors.textSecondary)),
                        Row(
                          children: [
                            for (var star = 1; star <= 5; star++)
                              IconButton(
                                tooltip: '$star od 5',
                                onPressed: () => setState(() {
                                  _rating = star;
                                  clearServerError('rating');
                                }),
                                icon: Icon(
                                  star <= _rating ? Icons.star_rounded : Icons.star_outline_rounded,
                                  color: star <= _rating ? AppColors.warning : AppColors.textSecondary,
                                  size: 34,
                                ),
                              ),
                          ],
                        ),
                        if ((_ratingTouched && _rating == 0) || serverError('rating') != null)
                          Padding(
                            padding: const EdgeInsets.only(left: 12, bottom: 8),
                            child: Text(
                              serverError('rating') ?? 'Odaberite ocjenu od 1 do 5 zvjezdica.',
                              style: const TextStyle(color: AppColors.danger, fontSize: 12),
                            ),
                          ),
                        const SizedBox(height: 8),
                        TextFormField(
                          controller: _comment,
                          maxLines: 4,
                          maxLength: Validators.reviewCommentMaxLength,
                          decoration: const InputDecoration(
                            labelText: 'Vaš komentar (nije obavezan)',
                            hintText: 'Napišite svoje iskustvo... (brzina, profesionalnost, kvalitet)',
                            alignLabelWithHint: true,
                          ),
                          validator: (v) => Validators.maxLength(v, Validators.reviewCommentMaxLength, 'Komentar'),
                          forceErrorText: serverError('comment'),
                          onChanged: (_) => clearServerError('comment'),
                        ),
                        const SizedBox(height: 8),
                        FormErrorText(formError),
                        BusyButton(
                          label: 'Objavi recenziju',
                          icon: Icons.edit_outlined,
                          busy: busy,
                          onPressed: () => unawaited(_publish()),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
