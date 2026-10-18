import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/desktop.dart';
import '../../core/widgets/dialogs.dart';
import '../../core/widgets/paged_view.dart';
import '../../data/models/employee.dart';
import 'employee_dialogs.dart';

class EmployeesScreen extends ConsumerStatefulWidget {
  const EmployeesScreen({super.key});

  @override
  ConsumerState<EmployeesScreen> createState() => _EmployeesScreenState();
}

class _EmployeesScreenState extends ConsumerState<EmployeesScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  var _term = '';
  bool? _active = true;
  var _version = 0;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _reload() => setState(() => _version++);

  Future<void> _open(Widget dialog) async {
    final saved = await showDialog<bool>(context: context, builder: (_) => dialog);
    if (saved ?? false) {
      _reload();
    }
  }

  Future<void> _remove(Employee employee) async {
    final ok = await confirm(
      context,
      title: 'Obrisati zaposlenika?',
      message: '${employee.fullName} bit će uklonjen, a njegov nalog zatvoren.',
      confirmLabel: 'Obriši',
      destructive: true,
    );
    if (!ok || !mounted) {
      return;
    }
    try {
      await ref.read(employeeRepositoryProvider).delete(employee.id);
      if (mounted) {
        showMessage(context, '${employee.fullName} je obrisan.');
        _reload();
      }
    } on ApiException catch (error) {
      // Someone with appointments cannot be removed; the server says so, and offers the way out.
      if (mounted && error.statusCode == 409) {
        final deactivate = await confirm(
          context,
          title: 'Brisanje nije moguće',
          message: '${error.message}\n\nDeaktivirati nalog sada?',
          confirmLabel: 'Deaktiviraj',
          destructive: true,
        );
        if (deactivate) {
          await _setActive(employee, active: false);
        }
      } else if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  Future<void> _setActive(Employee employee, {required bool active}) async {
    try {
      await ref
          .read(userRepositoryProvider)
          .setStatus(employee.userId, isActive: active, reason: active ? null : 'Deaktiviran kao zaposlenik.');
      if (mounted) {
        showMessage(
          context,
          active ? '${employee.fullName} je ponovo aktivan.' : '${employee.fullName} je deaktiviran.',
        );
        _reload();
      }
    } on ApiException catch (error) {
      if (mounted) {
        showMessage(context, error.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final repository = ref.watch(employeeRepositoryProvider);

    return ScreenScaffold(
      title: 'Upravljanje zaposlenicima',
      subtitle: 'Pregledajte i upravljajte timom',
      actions: [
        FilledButton.icon(
          onPressed: () => _open(const EmployeeFormDialog()),
          icon: const Icon(Icons.add),
          label: const Text('Dodaj zaposlenika'),
        ),
      ],
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      decoration: const InputDecoration(
                        hintText: 'Pretraži zaposlenike...',
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
                    child: DropdownButtonFormField<bool?>(
                      initialValue: _active,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: const [
                        DropdownMenuItem(child: Text('Svi')),
                        DropdownMenuItem(value: true, child: Text('Aktivni')),
                        DropdownMenuItem(value: false, child: Text('Neaktivni')),
                      ],
                      onChanged: (v) => setState(() => _active = v),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: PagedView<Employee>(
              reloadKey: (_term, _active, _version),
              fetch: (page) => repository.list(search: _term, isActive: _active, page: page),
              emptyMessage: 'Nema zaposlenika koji odgovaraju pretrazi.',
              builder: (context, items, _) => LayoutBuilder(
                builder: (context, constraints) {
                  final columns = constraints.maxWidth > 1300 ? 3 : (constraints.maxWidth > 850 ? 2 : 1);
                  final width = (constraints.maxWidth - 16 * (columns - 1)) / columns;
                  return Wrap(
                    spacing: 16,
                    runSpacing: 16,
                    children: [
                      for (final employee in items)
                        SizedBox(
                          width: width,
                          child: _EmployeeCard(
                            employee: employee,
                            onEdit: () => _open(EmployeeFormDialog(employee: employee)),
                            onSchedule: () => _open(ScheduleDialog(employee: employee)),
                            onRemove: () => _remove(employee),
                            onActivate: () => _setActive(employee, active: true),
                          ),
                        ),
                    ],
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmployeeCard extends StatelessWidget {
  const _EmployeeCard({
    required this.employee,
    required this.onEdit,
    required this.onSchedule,
    required this.onRemove,
    required this.onActivate,
  });

  final Employee employee;
  final VoidCallback onEdit;
  final VoidCallback onSchedule;
  final VoidCallback onRemove;
  final VoidCallback onActivate;

  @override
  Widget build(BuildContext context) {
    final years = employee.yearsOfService(DateTime.now().toUtc());
    final yearsLabel = years == 1 ? 'godina' : (years >= 2 && years <= 4 ? 'godine' : 'godina');

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 26,
                  backgroundColor: AppColors.accentSoft,
                  child: Text(
                    employee.initials,
                    style: const TextStyle(color: AppColors.accent, fontWeight: FontWeight.w800),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(employee.fullName, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
                      Text(employee.position, style: const TextStyle(color: AppColors.textSecondary)),
                    ],
                  ),
                ),
                Pill(
                  label: employee.isActive ? 'Aktivan' : 'Neaktivan',
                  color: employee.isActive ? AppColors.success : AppColors.textSecondary,
                ),
              ],
            ),
            const SizedBox(height: 16),
            _Line(icon: Icons.mail_outline, text: employee.email),
            _Line(icon: Icons.phone_outlined, text: employee.phoneNumber),
            _Line(
              icon: Icons.work_outline,
              text: '${employee.specialization ?? 'Bez specijalizacije'} • $years $yearsLabel',
            ),
            if (!employee.isAvailableForAssignment)
              const _Line(icon: Icons.do_not_disturb_on_outlined, text: 'Trenutno ne prima nove poslove'),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _Figure(label: 'Završeni poslovi', value: '${employee.completedJobs}'),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _Figure(
                    label: 'Ocjena',
                    value: employee.averageRating == null ? '—' : employee.averageRating!.toStringAsFixed(1),
                    color: AppColors.warning,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onEdit,
                    icon: const Icon(Icons.edit_outlined, size: 18),
                    label: const Text('Uredi'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.outlined(tooltip: 'Radno vrijeme', onPressed: onSchedule, icon: const Icon(Icons.schedule)),
                const SizedBox(width: 8),
                employee.isActive
                    ? IconButton.outlined(
                        tooltip: 'Obriši ili deaktiviraj',
                        color: AppColors.danger,
                        onPressed: onRemove,
                        icon: const Icon(Icons.delete_outline),
                      )
                    : IconButton.outlined(
                        tooltip: 'Ponovo aktiviraj',
                        color: AppColors.success,
                        onPressed: onActivate,
                        icon: const Icon(Icons.restart_alt),
                      ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Row(
      children: [
        Icon(icon, size: 16, color: AppColors.textSecondary),
        const SizedBox(width: 8),
        Expanded(child: Text(text, overflow: TextOverflow.ellipsis)),
      ],
    ),
  );
}

class _Figure extends StatelessWidget {
  const _Figure({required this.label, required this.value, this.color = AppColors.textPrimary});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(vertical: 10),
    decoration: BoxDecoration(color: AppColors.surfaceRaised, borderRadius: BorderRadius.circular(8)),
    child: Column(
      children: [
        Text(label, style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
        const SizedBox(height: 4),
        Text(
          value,
          style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: color),
        ),
      ],
    ),
  );
}
