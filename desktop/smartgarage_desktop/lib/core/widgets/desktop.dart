import 'package:flutter/material.dart';

import '../theme.dart';

/// A headline number, optionally with its change against last month.
class StatCard extends StatelessWidget {
  const StatCard({
    required this.label,
    required this.value,
    this.icon,
    this.color = AppColors.accent,
    this.valueColor = AppColors.textPrimary,
    this.changePercent,
    this.changeMeaning = 'U odnosu na isti period prošlog mjeseca',
    super.key,
  });

  final String label;
  final String value;
  final IconData? icon;
  final Color color;
  final Color valueColor;
  final double? changePercent;

  /// What the change is measured against, shown on hover.
  final String changeMeaning;

  @override
  Widget build(BuildContext context) {
    final change = changePercent;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (icon != null || change != null)
              Row(
                children: [
                  if (icon != null)
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Icon(icon, color: color),
                    ),
                  const Spacer(),
                  if (change != null)
                    Tooltip(
                      message: changeMeaning,
                      child: Text(
                        '${change >= 0 ? '+' : ''}${change.toStringAsFixed(1)}%',
                        style: TextStyle(
                          color: change >= 0 ? AppColors.success : AppColors.danger,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
            if (icon != null || change != null) const SizedBox(height: 16),
            Text(label, style: const TextStyle(color: AppColors.textSecondary)),
            const SizedBox(height: 6),
            Text(
              value,
              style: TextStyle(fontSize: 26, fontWeight: FontWeight.w800, color: valueColor),
            ),
          ],
        ),
      ),
    );
  }
}

/// A button that, when the action is not available, stays visible but disabled and says why on
/// hover - the instructions ask for exactly that instead of hiding it.
class GuardedButton extends StatelessWidget {
  const GuardedButton({
    required this.label,
    required this.onPressed,
    this.icon,
    this.disabledReason,
    this.destructive = false,
    this.filled = false,
    super.key,
  });

  final String label;
  final IconData? icon;

  /// Null makes the button disabled; [disabledReason] then explains it.
  final VoidCallback? onPressed;
  final String? disabledReason;
  final bool destructive;
  final bool filled;

  @override
  Widget build(BuildContext context) {
    final child = icon == null ? Text(label) : null;
    final Widget button = filled
        ? (icon == null
              ? FilledButton(onPressed: onPressed, child: child)
              : FilledButton.icon(onPressed: onPressed, icon: Icon(icon, size: 18), label: Text(label)))
        : (icon == null
              ? OutlinedButton(
                  style: destructive ? OutlinedButton.styleFrom(foregroundColor: AppColors.danger) : null,
                  onPressed: onPressed,
                  child: child,
                )
              : OutlinedButton.icon(
                  style: destructive ? OutlinedButton.styleFrom(foregroundColor: AppColors.danger) : null,
                  onPressed: onPressed,
                  icon: Icon(icon, size: 18),
                  label: Text(label),
                ));

    final reason = disabledReason;
    return onPressed == null && reason != null ? Tooltip(message: reason, child: button) : button;
  }
}

/// Column layout for the hand-built tables: a label and how much of the width it takes.
class TableColumn<T> {
  const TableColumn(this.label, this.cell, {this.flex = 1});

  final String label;
  final Widget Function(T item) cell;
  final int flex;
}

/// A simple table with a header row and clickable rows.
class SimpleTable<T> extends StatelessWidget {
  const SimpleTable({required this.columns, required this.items, this.onTap, super.key});

  final List<TableColumn<T>> columns;
  final List<T> items;
  final void Function(T item)? onTap;

  @override
  Widget build(BuildContext context) => Card(
    clipBehavior: Clip.antiAlias,
    child: Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          child: Row(
            children: [
              for (final column in columns)
                Expanded(
                  flex: column.flex,
                  child: Text(
                    column.label,
                    style: const TextStyle(color: AppColors.textSecondary, fontWeight: FontWeight.w700),
                  ),
                ),
            ],
          ),
        ),
        const Divider(),
        for (final item in items) ...[
          InkWell(
            onTap: onTap == null ? null : () => onTap!(item),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              child: Row(
                children: [
                  for (final column in columns)
                    Expanded(
                      flex: column.flex,
                      child: Padding(padding: const EdgeInsets.only(right: 12), child: column.cell(item)),
                    ),
                ],
              ),
            ),
          ),
          const Divider(),
        ],
      ],
    ),
  );
}

/// The standard page of the desktop: title, subtitle, optional actions on the right.
class ScreenScaffold extends StatelessWidget {
  const ScreenScaffold({required this.title, required this.body, this.subtitle, this.actions = const [], super.key});

  final String title;
  final String? subtitle;
  final List<Widget> actions;
  final Widget body;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(28, 24, 28, 0),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w800)),
                  if (subtitle != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(subtitle!, style: const TextStyle(color: AppColors.textSecondary)),
                    ),
                ],
              ),
            ),
            for (final action in actions) Padding(padding: const EdgeInsets.only(left: 12), child: action),
          ],
        ),
        const SizedBox(height: 20),
        Expanded(child: body),
      ],
    ),
  );
}
