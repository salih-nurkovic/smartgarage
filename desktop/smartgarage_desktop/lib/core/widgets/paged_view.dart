import 'package:flutter/material.dart';

import '../../data/models/common.dart';
import '../api/api_exception.dart';
import '../theme.dart';
import 'common.dart';

/// Reads a paged endpoint one page at a time and shows it through [builder], with the page
/// controls underneath.
///
/// Changing [reloadKey] goes back to the first page; screens pass their filters and a version
/// number that is bumped when the data changes, so a new record shows without a manual refresh.
class PagedView<T> extends StatefulWidget {
  const PagedView({required this.fetch, required this.builder, required this.emptyMessage, this.reloadKey, super.key});

  final Future<Paged<T>> Function(int page) fetch;
  final Widget Function(BuildContext context, List<T> items, VoidCallback reload) builder;
  final String emptyMessage;
  final Object? reloadKey;

  @override
  State<PagedView<T>> createState() => _PagedViewState<T>();
}

class _PagedViewState<T> extends State<PagedView<T>> {
  Paged<T>? _data;
  var _page = 1;
  var _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load(1);
  }

  @override
  void didUpdateWidget(PagedView<T> oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.reloadKey != widget.reloadKey) {
      _load(1);
    }
  }

  Future<void> _load(int page) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await widget.fetch(page);
      if (mounted) {
        setState(() {
          _data = data;
          _page = data.page;
        });
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() => _error = errorMessage(error));
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;

    if (data == null) {
      return _error != null
          ? ErrorView(message: _error!, onRetry: () => _load(_page))
          : const Center(child: CircularProgressIndicator());
    }

    final pages = data.totalCount == 0 ? 1 : (data.totalCount / data.pageSize).ceil();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_loading) const LinearProgressIndicator(minHeight: 2) else const SizedBox(height: 2),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
          ),
        Expanded(
          child: data.items.isEmpty
              ? EmptyView(icon: Icons.inbox_outlined, message: widget.emptyMessage)
              : SingleChildScrollView(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: widget.builder(context, data.items, () => _load(_page)),
                ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 10),
          child: Row(
            children: [
              Text('Ukupno: ${data.totalCount}', style: const TextStyle(color: AppColors.textSecondary)),
              const Spacer(),
              IconButton(
                tooltip: 'Prethodna stranica',
                onPressed: _page > 1 && !_loading ? () => _load(_page - 1) : null,
                icon: const Icon(Icons.chevron_left),
              ),
              Text('Stranica $_page od $pages'),
              IconButton(
                tooltip: 'Sljedeća stranica',
                onPressed: data.hasMore && !_loading ? () => _load(_page + 1) : null,
                icon: const Icon(Icons.chevron_right),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
