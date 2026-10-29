import 'package:flutter/material.dart';

/// Abas da área de quem NÃO é Admin (perfis Colaborador e RH), na ordem do menu lateral.
/// O Admin continua com as 7 abas de `AbaAdmin` (menu_icones_admin.dart).
enum AbaPerfil {
  inicio('Início', 'Resumo do seu dia', Icons.home_rounded),
  ponto('Registro de ponto', 'Bater o ponto e ver o espelho', Icons.fingerprint_rounded),
  holerites('Holerites', 'Contracheques em PDF', Icons.receipt_long_rounded),
  funcionarios('Funcionários', 'Busca pelo código FUNC', Icons.groups_rounded),
  novoFuncionario('Novo funcionário', 'Cadastro de colaborador', Icons.person_add_alt_1_rounded),
  advertencias('Gestão de advertências', 'Semáforo e régua', Icons.gavel_rounded),
  configuracoes('Configurações', 'Conta, privacidade e canal com o RH', Icons.settings_rounded);

  const AbaPerfil(this.rotulo, this.descricao, this.icone);

  final String rotulo;
  final String descricao;
  final IconData icone;

  /// Abas que só o RH enxerga (o servidor também recusa quem não tem o perfil).
  static const Set<AbaPerfil> soRh = {funcionarios, novoFuncionario, advertencias};

  /// Abas liberadas para o [perfil] ('Colaborador' ou 'RH'); qualquer outro valor cai no menor conjunto.
  static List<AbaPerfil> doPerfil(String perfil) => [
        for (final aba in values)
          if (perfil == 'RH' || !soRh.contains(aba)) aba,
      ];
}
