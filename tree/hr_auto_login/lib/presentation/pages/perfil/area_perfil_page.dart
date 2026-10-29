import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../providers.dart';
import '../../widgets/perfil/abas_perfil.dart';
import '../../widgets/perfil/barra_superior_perfil.dart';
import '../../widgets/perfil/menu_icones_perfil.dart';
import '../admin/aba_advertencias.dart';
import '../admin/aba_funcionarios.dart';
import '../admin/aba_holerites.dart';
import '../admin/aba_novo_funcionario.dart';
import '../admin/aba_registro_ponto.dart';
import 'aba_configuracoes_perfil.dart';
import 'aba_inicio.dart';

/// Área dos perfis Colaborador e RH (o Admin tem a `AdminPage`).
/// Mesmo modelo do Admin: menu lateral só com ícones (RNF08), uma aba por vez e o mesmo layout
/// no celular e no computador.
///
///  - Colaborador: Início, Registro de ponto, Holerites e Configurações.
///  - RH: as mesmas, mais Funcionários, Novo funcionário e Gestão de advertências.
///
/// O que cada perfil pode fazer é decidido pelo servidor; este menu só evita mostrar o que seria recusado.
class AreaPerfilPage extends ConsumerStatefulWidget {
  const AreaPerfilPage({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  ConsumerState<AreaPerfilPage> createState() => _AreaPerfilPageState();
}

class _AreaPerfilPageState extends ConsumerState<AreaPerfilPage> {
  AbaPerfil _aba = AbaPerfil.inicio;

  /// Código do funcionário recém-cadastrado, destacado ao abrir a aba Funcionários.
  String? _destaque;

  late final List<AbaPerfil> _abas = AbaPerfil.doPerfil(widget.usuario.perfil);

  void _abrir(AbaPerfil aba, {String? destaque}) {
    // Atalhos só levam a abas liberadas para o perfil.
    if (!_abas.contains(aba)) return;
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    setState(() {
      _aba = aba;
      _destaque = destaque;
    });
  }

  void _sair() => ref.read(sessaoProvider.notifier).state = null;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final compacto = limites.maxWidth < 600;

        return Scaffold(
          backgroundColor: AppColors.fundo,
          body: Row(
            children: [
              MenuIconesPerfil(
                usuario: widget.usuario,
                abas: _abas,
                abaAtiva: _aba,
                compacto: compacto,
                onSelecionar: _abrir,
                onSair: _sair,
              ),
              Expanded(
                child: SafeArea(
                  left: false,
                  child: Column(
                    children: [
                      BarraSuperiorPerfil(usuario: widget.usuario, aba: _aba, hoje: DateTime.now()),
                      Expanded(
                        child: AnimatedSwitcher(
                          duration: const Duration(milliseconds: 200),
                          child: KeyedSubtree(key: ValueKey(_aba), child: _conteudo()),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _conteudo() => switch (_aba) {
        AbaPerfil.inicio => AbaInicio(usuario: widget.usuario, onAbrir: _abrir),
        AbaPerfil.ponto => AbaRegistroPonto(usuario: widget.usuario),
        AbaPerfil.holerites => AbaHolerites(usuario: widget.usuario),
        AbaPerfil.funcionarios => AbaFuncionarios(
            usuario: widget.usuario,
            destaque: _destaque,
            onNovo: () => _abrir(AbaPerfil.novoFuncionario),
          ),
        AbaPerfil.novoFuncionario => AbaNovoFuncionario(
            onCadastrado: (codigo) => _abrir(AbaPerfil.funcionarios, destaque: codigo),
            onCancelar: () => _abrir(AbaPerfil.funcionarios),
          ),
        AbaPerfil.advertencias => AbaAdvertencias(usuario: widget.usuario),
        AbaPerfil.configuracoes => AbaConfiguracoesPerfil(usuario: widget.usuario, onSair: _sair),
      };
}
