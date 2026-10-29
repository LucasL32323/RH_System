import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_colors.dart';

/// Tema único do app (Material 3 + Inter).
class AppTheme {
  AppTheme._();

  static const double raioCampo = 12;
  static const double raioCard = 20;

  /// [usarGoogleFonts] = false nos testes de widget (o teste não tem internet para baixar a Inter).
  static ThemeData claro({bool usarGoogleFonts = true}) {
    final textoBase = ThemeData.light(useMaterial3: true).textTheme;
    final texto = (usarGoogleFonts ? GoogleFonts.interTextTheme(textoBase) : textoBase)
        .apply(bodyColor: AppColors.texto, displayColor: AppColors.texto);

    final esquema = ColorScheme.fromSeed(seedColor: AppColors.primaria).copyWith(
      primary: AppColors.primaria,
      onPrimary: Colors.white,
      secondary: AppColors.secundaria,
      onSecondary: Colors.white,
      tertiary: AppColors.destaque,
      surface: AppColors.superficie,
      onSurface: AppColors.texto,
      error: AppColors.erro,
    );

    OutlineInputBorder borda(Color cor, [double largura = 1]) => OutlineInputBorder(
          borderRadius: BorderRadius.circular(raioCampo),
          borderSide: BorderSide(color: cor, width: largura),
        );

    return ThemeData(
      useMaterial3: true,
      colorScheme: esquema,
      scaffoldBackgroundColor: AppColors.fundo,
      textTheme: texto,
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.campo,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        hintStyle: const TextStyle(color: AppColors.textoSuave, fontWeight: FontWeight.w400),
        prefixIconColor: AppColors.textoSuave,
        suffixIconColor: AppColors.textoSuave,
        errorMaxLines: 3,
        border: borda(AppColors.borda),
        enabledBorder: borda(AppColors.borda),
        focusedBorder: borda(AppColors.primaria, 1.6),
        errorBorder: borda(AppColors.erro),
        focusedErrorBorder: borda(AppColors.erro, 1.6),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.primaria,
          foregroundColor: Colors.white,
          disabledBackgroundColor: AppColors.primariaDesabilitada,
          disabledForegroundColor: Colors.white,
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(raioCampo)),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600, letterSpacing: 0.2),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.primaria,
          minimumSize: const Size.fromHeight(48),
          side: const BorderSide(color: AppColors.borda),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(raioCampo)),
          textStyle: const TextStyle(fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primaria,
          textStyle: const TextStyle(fontWeight: FontWeight.w600),
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: AppColors.texto,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(raioCampo)),
      ),
      // O estilo do diálogo fica no próprio widget (o tipo de dialogTheme mudou entre versões do Flutter).
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.primaria),
    );
  }
}
