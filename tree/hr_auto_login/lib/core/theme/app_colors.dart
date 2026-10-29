import 'package:flutter/material.dart';

/// Paleta do protótipo Figma (tons petróleo/teal).
/// Contraste: texto branco só sobre [primaria] ou [secundaria]; [destaque] e [apoio]
/// são usados em detalhes e elementos decorativos, nunca como fundo de texto pequeno.
class AppColors {
  AppColors._();

  // ---- Paleta definida no Figma ----
  static const Color fundo = Color(0xFFFFFFFA); // #FFFFFA - fundo geral
  static const Color primaria = Color(0xFF0D5C63); // #0D5C63 - botões, links, foco
  static const Color secundaria = Color(0xFF247B7B); // #247B7B - gradientes, hover
  static const Color destaque = Color(0xFF44A1A0); // #44A1A0 - ícones e detalhes
  static const Color apoio = Color(0xFF78CDD7); // #78CDD7 - decoração e realces suaves

  // ---- Derivadas (neutros e estados) ----
  static const Color superficie = Color(0xFFFFFFFF);
  static const Color texto = Color(0xFF0B2E31); // petróleo quase preto (contraste AAA no fundo)
  static const Color textoSuave = Color(0xFF4F6B6D);
  static const Color borda = Color(0xFFD6E5E6);
  static const Color campo = Color(0xFFFDFEFC);
  static const Color erro = Color(0xFFB3261E);
  static const Color erroFundo = Color(0xFFFCEDEC);
  static const Color sucesso = Color(0xFF1F7A4D);
  static const Color sucessoFundo = Color(0xFFE7F4EE);
  static const Color infoFundo = Color(0xFFE9F6F7);
  static const Color aviso = Color(0xFF8A5A00); // âmbar escuro (contraste AA sobre avisoFundo)
  static const Color avisoFundo = Color(0xFFFDF3DC);
  static const Color alerta = Color(0xFFC98A00); // âmbar do semáforo (ícones e barras, nunca texto)
  static const Color superficieSuave = Color(0xFFF2F8F8); // cabeçalho de tabela, trilhas de barra, hover
  static const Color primariaHover = Color(0xFF0A4B51);

  // ---- Transparências pré-calculadas (evita APIs de opacidade que mudam entre versões do Flutter) ----
  static const Color primariaDesabilitada = Color(0x800D5C63); // 50%
  static const Color sombraCard = Color(0x1A0D5C63); // 10%
  static const Color sombraFina = Color(0x0D000000); // 5%
  static const Color brancoTransl12 = Color(0x1FFFFFFF);
  static const Color brancoTransl14 = Color(0x24FFFFFF);
  static const Color brancoTransl20 = Color(0x33FFFFFF);
  static const Color brancoTransl60 = Color(0x99FFFFFF);
  static const Color brancoTransl80 = Color(0xCCFFFFFF);
  static const Color apoioTransl18 = Color(0x2E78CDD7);
  static const Color apoioTransl10 = Color(0x1A78CDD7);
}
