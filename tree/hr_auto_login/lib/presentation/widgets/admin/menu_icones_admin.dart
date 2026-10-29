import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import 'componentes_admin.dart';

/// Abas do Admin, na ordem do menu (protótipos "HR-Auto | Protótipos").
enum AbaAdmin {
  dashboard('Dashboard', 'Painel da empresa', Icons.space_dashboard_rounded),
  ponto('Registro de ponto', 'Bater o ponto e ver o espelho', Icons.fingerprint_rounded),
  holerites('Holerites', 'Contracheques em PDF', Icons.receipt_long_rounded),
  funcionarios('Funcionários', 'Busca pelo código FUNC', Icons.groups_rounded),
  novoFuncionario('Novo funcionário', 'Cadastro de colaborador', Icons.person_add_alt_1_rounded),
  compliance('Central de Compliance', 'Leituras, testes e benefícios', Icons.verified_user_rounded),
  advertencias('Gestão de advertências', 'Semáforo e régua', Icons.gavel_rounded),
  configuracoes('Configurações', 'Conta e estrutura da empresa', Icons.settings_rounded);

  const AbaAdmin(this.rotulo, this.descricao, this.icone);
  final String rotulo;
  final String descricao;
  final IconData icone;
}

/// Menu lateral só com ícones (RNF08): uma aba por vez e o mesmo layout no celular e no computador.
/// O nome de cada aba aparece no tooltip e no leitor de tela.
class MenuIconesAdmin extends StatelessWidget {
  const MenuIconesAdmin({
    super.key,
    required this.usuario,
    required this.abaAtiva,
    required this.onSelecionar,
    required this.onSair,
    this.pendencias = const {},
    this.compacto = false,
  });

  final UsuarioAutenticado usuario;
  final AbaAdmin abaAtiva;
  final ValueChanged<AbaAdmin> onSelecionar;
  final VoidCallback onSair;

  /// Contador no canto do ícone (ex.: ajustes de ponto pendentes no Dashboard).
  final Map<AbaAdmin, int> pendencias;

  /// Celular: trilho de 64 px em vez de 80 px.
  final bool compacto;

  static double larguraPara({required bool compacto}) => compacto ? 64 : 80;

  @override
  Widget build(BuildContext context) {
    final tamanhoItem = compacto ? 44.0 : 48.0;

    return Container(
      width: larguraPara(compacto: compacto),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [AppColors.primaria, AppColors.secundaria],
        ),
      ),
      child: SafeArea(
        right: false,
        child: Column(
          children: [
            SizedBox(
              height: 72,
              child: Center(
                child: Semantics(
                  label: 'HR-Auto',
                  excludeSemantics: true,
                  child: Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: AppColors.brancoTransl20,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: AppColors.brancoTransl20),
                    ),
                    child: const Icon(Icons.badge_rounded, color: Colors.white, size: 22),
                  ),
                ),
              ),
            ),
            Container(height: 1, margin: const EdgeInsets.symmetric(horizontal: 16), color: AppColors.brancoTransl12),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(vertical: 16),
                child: Column(
                  children: [
                    for (final aba in AbaAdmin.values) ...[
                      if (aba == AbaAdmin.configuracoes)
                        Container(
                          height: 1,
                          width: 32,
                          margin: const EdgeInsets.symmetric(vertical: 8),
                          color: AppColors.brancoTransl12,
                        ),
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: _ItemIcone(
                          aba: aba,
                          ativo: aba == abaAtiva,
                          tamanho: tamanhoItem,
                          contador: pendencias[aba] ?? 0,
                          onTap: () => onSelecionar(aba),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Column(
                children: [
                  Tooltip(
                    message: '${usuario.nome}\n${usuario.codigo} · ${usuario.perfil}',
                    child: AvatarIniciais(
                      iniciais: iniciaisDoUsuario(usuario.nome),
                      fundo: AppColors.brancoTransl20,
                      cor: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 8),
                  IconButton(
                    onPressed: onSair,
                    tooltip: 'Sair',
                    icon: const Icon(Icons.logout_rounded, size: 20, color: AppColors.brancoTransl80),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Iniciais do nome do usuário logado: primeiro e último nome, sem "da/de/do"
/// ("Administrador do Sistema" -> "AS").
String iniciaisDoUsuario(String nome) {
  const conectivos = {'da', 'das', 'de', 'do', 'dos', 'e'};
  final partes = nome
      .trim()
      .split(RegExp(r'\s+'))
      .where((p) => p.isNotEmpty && !conectivos.contains(p.toLowerCase()))
      .toList();
  if (partes.isEmpty) return '?';
  final ultima = partes.length > 1 ? partes.last[0] : '';
  return (partes.first[0] + ultima).toUpperCase();
}

class _ItemIcone extends StatelessWidget {
  const _ItemIcone({
    required this.aba,
    required this.ativo,
    required this.tamanho,
    required this.contador,
    required this.onTap,
  });

  final AbaAdmin aba;
  final bool ativo;
  final double tamanho;
  final int contador;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final icone = Icon(aba.icone, size: 22, color: ativo ? Colors.white : AppColors.brancoTransl60);

    return Tooltip(
      message: aba.rotulo,
      child: Semantics(
        label: contador > 0 ? '${aba.rotulo}, $contador pendentes' : aba.rotulo,
        selected: ativo,
        button: true,
        excludeSemantics: true,
        child: Material(
          color: ativo ? AppColors.brancoTransl20 : Colors.transparent,
          borderRadius: BorderRadius.circular(AppTheme.raioCampo),
          child: InkWell(
            key: Key('aba_${aba.name}'),
            onTap: onTap,
            borderRadius: BorderRadius.circular(AppTheme.raioCampo),
            hoverColor: AppColors.brancoTransl12,
            splashColor: AppColors.brancoTransl12,
            child: SizedBox(
              width: tamanho,
              height: tamanho,
              child: Stack(
                alignment: Alignment.center,
                children: [
                  if (ativo)
                    Positioned(
                      left: 0,
                      top: tamanho * 0.25,
                      bottom: tamanho * 0.25,
                      child: Container(
                        width: 3,
                        decoration: BoxDecoration(color: AppColors.apoio, borderRadius: BorderRadius.circular(2)),
                      ),
                    ),
                  if (contador > 0)
                    Badge(
                      label: Text('$contador', style: const TextStyle(fontFeatures: numerosTabulares)),
                      backgroundColor: AppColors.apoio,
                      textColor: AppColors.texto,
                      child: icone,
                    )
                  else
                    icone,
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
