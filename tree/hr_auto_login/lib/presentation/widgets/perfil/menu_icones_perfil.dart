import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../admin/componentes_admin.dart';
import '../admin/menu_icones_admin.dart' show iniciaisDoUsuario;
import 'abas_perfil.dart';

/// Menu lateral só com ícones (RNF08) das áreas Colaborador e RH: uma aba por vez,
/// mesmo layout no celular (trilho de 64 px) e no computador (80 px).
/// O nome de cada aba aparece no tooltip e no leitor de tela.
class MenuIconesPerfil extends StatelessWidget {
  const MenuIconesPerfil({
    super.key,
    required this.usuario,
    required this.abas,
    required this.abaAtiva,
    required this.onSelecionar,
    required this.onSair,
    this.compacto = false,
  });

  final UsuarioAutenticado usuario;
  final List<AbaPerfil> abas;
  final AbaPerfil abaAtiva;
  final ValueChanged<AbaPerfil> onSelecionar;
  final VoidCallback onSair;
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
                    for (final aba in abas) ...[
                      // Configurações fica separada das demais, como no menu do Admin.
                      if (aba == AbaPerfil.configuracoes)
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

class _ItemIcone extends StatelessWidget {
  const _ItemIcone({required this.aba, required this.ativo, required this.tamanho, required this.onTap});

  final AbaPerfil aba;
  final bool ativo;
  final double tamanho;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: aba.rotulo,
      child: Semantics(
        label: aba.rotulo,
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
                  Icon(aba.icone, size: 22, color: ativo ? Colors.white : AppColors.brancoTransl60),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
