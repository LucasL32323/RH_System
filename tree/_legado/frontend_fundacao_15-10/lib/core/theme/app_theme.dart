import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_colors.dart';

/// Tema global: azul corporativo + fonte Inter.
class AppTheme {
  AppTheme._();

  static ThemeData claro() {
    final esquema = ColorScheme.fromSeed(
      seedColor: AppColors.principal,
      primary: AppColors.principal,
      secondary: AppColors.destaque,
      surface: Colors.white,
      brightness: Brightness.light,
    );

    final base = ThemeData(
      useMaterial3: true,
      colorScheme: esquema,
      scaffoldBackgroundColor: AppColors.fundo,
    );

    return base.copyWith(
      textTheme: GoogleFonts.interTextTheme(base.textTheme).apply(
        bodyColor: AppColors.texto,
        displayColor: AppColors.texto,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.principal,
          foregroundColor: Colors.white,
        ),
      ),
    );
  }
}
