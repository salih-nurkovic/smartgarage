import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/formatting.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/admin.dart';

/// Recorded failures. The detail is the only place in the system where a stack trace is shown.
class ErrorLogScreen extends ConsumerStatefulWidget {
  const ErrorLogScreen({super.key});

  @override
  ConsumerState<ErrorLogScreen> createState() => _ErrorLogScreenState();
}

class _ErrorLogScreenState extends ConsumerState<ErrorLogScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  String? _source;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(errorLogRepositoryProvider);

    return ScreenScaffold(
      title: 'Log grešaka',
      subtitle: 'Neočekivane greške API-ja i Workera, sa oznakom za praćenje',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(
                    hintText: 'Pretraži po oznaci, poruci, tipu izuzetka ili putanji...',
                    prefixIcon: Icon(Icons.search),
                  ),
                  onChanged: (value) {
                    _debounce?.cancel();
                    _debounce = Timer(const Duration(milliseconds: 400), () => setState(() => _term = value));
                  },
                ),
              ),
              const SizedBox(width: 12),
              SizedBox(
                width: 180,
                child: DropdownButtonFormField<String?>(
                  initialValue: _source,
                  decoration: const InputDecoration(labelText: 'Izvor'),
                  items: const [
                    DropdownMenuItem(child: Text('Svi izvori')),
                    DropdownMenuItem(value: 'API', child: Text('API')),
                    DropdownMenuItem(value: 'Worker', child: Text('Worker')),
                  ],
                  onChanged: (v) => setState(() => _source = v),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<ErrorLogItem>(
              reloadKey: (_term, _source),
              fetch: (page) => repository.list(search: _term, source: _source, page: page),
              emptyMessage: 'Nema zabilježenih grešaka.',
              builder: (context, items, _) => SimpleTable<ErrorLogItem>(
                items: items,
                onTap: (e) => showDialog<void>(
                  context: context,
                  builder: (_) => _ErrorDialog(id: e.id),
                ),
                columns: [
                  TableColumn('Vrijeme', (e) => Text(Fmt.dateTime(e.occurredAtUtc))),
                  TableColumn('Izvor', (e) => Text(e.source)),
                  TableColumn(
                    'Zahtjev',
                    (e) =>
                        Text('${e.httpMethod ?? ''} ${e.requestPath ?? '—'}'.trim(), overflow: TextOverflow.ellipsis),
                    flex: 2,
                  ),
                  TableColumn('Poruka', (e) => Text(e.message, maxLines: 2, overflow: TextOverflow.ellipsis), flex: 3),
                  TableColumn(
                    'Oznaka',
                    (e) => SelectableText(e.correlationId, style: const TextStyle(fontSize: 12)),
                    flex: 2,
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

final _errorProvider = FutureProvider.autoDispose.family<ErrorLogItem, int>(
  (ref, id) => ref.watch(errorLogRepositoryProvider).get(id),
);

class _ErrorDialog extends ConsumerWidget {
  const _ErrorDialog({required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entry = ref.watch(_errorProvider(id));

    return AlertDialog(
      titlePadding: const EdgeInsets.fromLTRB(24, 16, 12, 0),
      title: Row(
        children: [
          const Expanded(child: Text('Detalji greške')),
          IconButton(tooltip: 'Zatvori', onPressed: () => Navigator.of(context).pop(), icon: const Icon(Icons.close)),
        ],
      ),
      content: SizedBox(
        width: 900,
        height: 560,
        child: AsyncBody<ErrorLogItem>(
          value: entry,
          onRetry: () => ref.invalidate(_errorProvider(id)),
          builder: (e) => ListView(
            children: [
              KeyValueRow(label: 'Vrijeme', value: Fmt.dateTime(e.occurredAtUtc)),
              KeyValueRow(label: 'Izvor', value: e.source),
              KeyValueRow(label: 'Oznaka', value: e.correlationId),
              KeyValueRow(label: 'Zahtjev', value: '${e.httpMethod ?? ''} ${e.requestPath ?? '—'}'.trim()),
              KeyValueRow(label: 'HTTP status', value: '${e.statusCode ?? '—'}'),
              KeyValueRow(label: 'Korisnik', value: e.userName ?? '—'),
              KeyValueRow(label: 'Tip izuzetka', value: e.exceptionType ?? '—'),
              const SizedBox(height: 8),
              Text(e.message, style: const TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 12),
              Row(
                children: [
                  const Text('Stack trace', style: TextStyle(color: AppColors.textSecondary)),
                  const Spacer(),
                  TextButton.icon(
                    onPressed: e.stackTrace == null
                        ? null
                        : () async {
                            await Clipboard.setData(ClipboardData(text: e.stackTrace!));
                            if (context.mounted) {
                              showMessage(context, 'Stack trace je kopiran.');
                            }
                          },
                    icon: const Icon(Icons.copy, size: 16),
                    label: const Text('Kopiraj'),
                  ),
                ],
              ),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: AppColors.background, borderRadius: BorderRadius.circular(8)),
                child: SelectableText(
                  e.stackTrace ?? 'Nije zabilježen.',
                  style: const TextStyle(fontFamily: 'Consolas', fontSize: 12),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
