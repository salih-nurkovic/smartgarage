import 'package:flutter/material.dart';

import '../../data/models/common.dart';
import '../api/api_exception.dart';
import 'common.dart';

/// A list read page by page from a paged endpoint, with pull-to-refresh and "load more".
///
/// Changing [reloadKey] reloads from the first page; screens pass a version number that is
/// bumped when the data changes elsewhere, so a new record appears without a manual refresh.
class PagedList<T> extends StatefulWidget {
  const PagedList({
    required this.fetch,
    required this.itemBuilder,
    required this.empty,
    this.reloadKey,
    this.header,
    this.padding = const EdgeInsets.fromLTRB(16, 0, 16, 24),
    super.key,
  });

  final Future<Paged<T>> Function(int page) fetch;
  final Widget Function(BuildContext context, T item, VoidCallback reload) itemBuilder;
  final Widget empty;
  final Object? reloadKey;
  final Widget? header;
  final EdgeInsets padding;

  @override
  State<PagedList<T>> createState() => _PagedListState<T>();
}

class _PagedListState<T> extends State<PagedList<T>> {
  final _items = <T>[];
  var _page = 0;
  var _hasMore = true;
  var _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  @override
  void didUpdateWidget(PagedList<T> oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.reloadKey != widget.reloadKey) {
      _reload();
    }
  }

  Future<void> _reload() async {
    _page = 0;
    _hasMore = true;
    await _loadNext(replace: true);
  }

  Future<void> _loadNext({bool replace = false}) async {
    if (_loading) {
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final result = await widget.fetch(_page + 1);
      if (!mounted) {
        return;
      }
      setState(() {
        if (replace) {
          _items.clear();
        }
        _items.addAll(result.items);
        _page = result.page;
        _hasMore = result.hasMore;
      });
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
    if (_items.isEmpty && _loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_items.isEmpty && _error != null) {
      return ErrorView(message: _error!, onRetry: _reload);
    }

    final header = widget.header;
    return RefreshIndicator(
      onRefresh: _reload,
      child: ListView(
        padding: widget.padding,
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          ?header,
          if (_items.isEmpty) Padding(padding: const EdgeInsets.only(top: 48), child: widget.empty),
          for (final item in _items)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: widget.itemBuilder(context, item, _reload),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(_error!, textAlign: TextAlign.center),
            ),
          if (_hasMore && _items.isNotEmpty)
            Center(
              child: _loading
                  ? const CircularProgressIndicator()
                  : TextButton(onPressed: _loadNext, child: const Text('Učitaj još')),
            ),
        ],
      ),
    );
  }
}
