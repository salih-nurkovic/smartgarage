import 'package:flutter/material.dart';

/// The palette of the approved desktop mockups: near-black navy, one blue accent, muted status
/// colours.
abstract final class AppColors {
  static const background = Color(0xFF070B14);
  static const surface = Color(0xFF0E1525);
  static const surfaceRaised = Color(0xFF131C2E);
  static const border = Color(0xFF1E293B);
  static const accent = Color(0xFF2563EB);
  static const accentSoft = Color(0xFF15254A);
  static const textPrimary = Color(0xFFF1F5F9);
  static const textSecondary = Color(0xFF94A3B8);
  static const success = Color(0xFF22C55E);
  static const warning = Color(0xFFF59E0B);
  static const urgent = Color(0xFFF97316);
  static const danger = Color(0xFFEF4444);
  static const violet = Color(0xFF8B5CF6);

  /// For charts: one colour per series, in order.
  static const series = [accent, Color(0xFF10B981), warning, danger, violet];
}

abstract final class AppTheme {
  static const radius = 12.0;

  static ThemeData dark() {
    final scheme = ColorScheme.fromSeed(seedColor: AppColors.accent, brightness: Brightness.dark).copyWith(
      primary: AppColors.accent,
      onPrimary: Colors.white,
      surface: AppColors.surface,
      onSurface: AppColors.textPrimary,
      error: AppColors.danger,
      outline: AppColors.border,
    );
    final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(radius));
    OutlineInputBorder border(Color color, [double width = 1]) => OutlineInputBorder(
      borderRadius: BorderRadius.circular(radius),
      borderSide: BorderSide(color: color, width: width),
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: AppColors.background,
      visualDensity: VisualDensity.standard,
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.background,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
        scrolledUnderElevation: 0,
      ),
      cardTheme: CardThemeData(
        color: AppColors.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radius),
          side: const BorderSide(color: AppColors.border),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.surface,
        isDense: true,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        hintStyle: const TextStyle(color: AppColors.textSecondary),
        border: border(AppColors.border),
        enabledBorder: border(AppColors.border),
        focusedBorder: border(AppColors.accent, 1.5),
        errorMaxLines: 3,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 44),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          shape: shape,
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 44),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          shape: shape,
          backgroundColor: AppColors.surfaceRaised,
          foregroundColor: AppColors.textPrimary,
          side: const BorderSide(color: AppColors.border),
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      navigationRailTheme: const NavigationRailThemeData(
        backgroundColor: AppColors.surface,
        indicatorColor: AppColors.accentSoft,
        selectedIconTheme: IconThemeData(color: AppColors.accent),
        unselectedIconTheme: IconThemeData(color: AppColors.textSecondary),
        selectedLabelTextStyle: TextStyle(color: AppColors.accent, fontWeight: FontWeight.w700),
        unselectedLabelTextStyle: TextStyle(color: AppColors.textSecondary),
      ),
      dataTableTheme: const DataTableThemeData(
        headingTextStyle: TextStyle(color: AppColors.textSecondary, fontWeight: FontWeight.w700),
        dividerThickness: 0.6,
      ),
      dividerTheme: const DividerThemeData(color: AppColors.border, space: 1),
      tooltipTheme: const TooltipThemeData(waitDuration: Duration(milliseconds: 300)),
      snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating, width: 520),
      dialogTheme: DialogThemeData(
        backgroundColor: AppColors.surface,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radius)),
      ),
    );
  }
}
