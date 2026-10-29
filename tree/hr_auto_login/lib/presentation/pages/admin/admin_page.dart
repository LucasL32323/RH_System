import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../providers.dart';
import '../../viewmodels/dashboard_admin_view_model.dart';
import '../../widgets/admin/barra_superior_admin.dart';
import '../../widgets/admin/menu_icones_admin.dart';
import '../../widgets/admin/pagina_admin.dart';
import '../../widgets/admin/paineis_dashboard.dart';
import 'aba_advertencias.dart';
import 'aba_compliance.dart';
import 'aba_configuracoes.dart';
import 'aba_dashboard.dart';
import 'aba_funcionarios.dart';
import 'aba_holerites.dart';
import 'aba_novo_funcionario.dart';
import 'aba_registro_ponto.dart';

/// Área do Administrador, no modelo dos protótipos "HR-Auto | Protótipos":
/// menu lateral só com ícones (RNF08) e uma aba por vez, com o mesmo layout no celular e no computador.
///
/// | Largura   | Menu                 | Conteúdo                                  |
/// |-----------|----------------------|-------------------------------------------|
/// | >= 600 px | trilho de 80 px      | margens de 32 px a partir de 900 px       |
/// | < 600 px  | trilho de 64 px      | margens de 16 px; tabelas rolam na horizontal |
class AdminPage extends ConsumerStatefulWidget {
  const AdminPage({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  ConsumerState<AdminPage> createState() => _AdminPageState();
}

class _AdminPageState extends ConsumerState<AdminPage> {
  AbaAdmin _aba = AbaAdmin.dashboard;

  /// Código do funcionário recém-cadastrado, destacado ao abrir a aba Funcionários.
  String? _destaque;

  void _abrir(AbaAdmin aba, {String? destaque}) {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    setState(() {
      _aba = aba;
      _destaque = destaque;
    });
  }

  void _sair() => ref.read(sessaoProvider.notifier).state = null;

  void _mostrarAlertas() {
    final dados = ref.read(dashboardAdminViewModelProvider).valueOrNull;
    if (dados == null) return;
    showDialog<void>(
      context: context,
      builder: (contexto) => DialogoAdmin(
        titulo: 'Alertas',
        subtitulo: 'Gerados pelas regras de horas extras, clima, ponto offline e régua de consequências.',
        rotuloConfirmar: 'Ver no Dashboard',
        onConfirmar: () {
          Navigator.of(contexto).pop();
          _abrir(AbaAdmin.dashboard);
        },
        conteudo: ListaAlertas(alertas: dados.alertas, referencia: dados.referencia),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final dados = ref.watch(dashboardAdminViewModelProvider).valueOrNull;

    return LayoutBuilder(
      builder: (context, limites) {
        final compacto = limites.maxWidth < 600;

        return Scaffold(
          backgroundColor: AppColors.fundo,
          body: Row(
            children: [
              MenuIconesAdmin(
                usuario: widget.usuario,
                abaAtiva: _aba,
                compacto: compacto,
                pendencias: {AbaAdmin.dashboard: dados?.ajustesPendentes ?? 0},
                onSelecionar: _abrir,
                onSair: _sair,
              ),
              Expanded(
                child: SafeArea(
                  left: false,
                  child: Column(
                    children: [
                      BarraSuperiorAdmin(
                        usuario: widget.usuario,
                        aba: _aba,
                        // Com a API, a barra mostra a data real; no protótipo, a data dos dados de exemplo.
                        hoje: AppConfig.modoPrototipo ? (dados?.referencia ?? DateTime.now()) : DateTime.now(),
                        alertas: dados?.alertas.length ?? 0,
                        onAlertas: _mostrarAlertas,
                      ),
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
        AbaAdmin.dashboard => AbaDashboard(onAbrir: _abrir),
        AbaAdmin.ponto => AbaRegistroPonto(usuario: widget.usuario),
        AbaAdmin.holerites => AbaHolerites(usuario: widget.usuario),
        AbaAdmin.funcionarios => AbaFuncionarios(
            usuario: widget.usuario,
            destaque: _destaque,
            onNovo: () => _abrir(AbaAdmin.novoFuncionario),
          ),
        AbaAdmin.novoFuncionario => AbaNovoFuncionario(
            onCadastrado: (codigo) => _abrir(AbaAdmin.funcionarios, destaque: codigo),
            onCancelar: () => _abrir(AbaAdmin.funcionarios),
          ),
        AbaAdmin.compliance => const AbaCompliance(),
        AbaAdmin.advertencias => AbaAdvertencias(usuario: widget.usuario),
        AbaAdmin.configuracoes => AbaConfiguracoes(usuario: widget.usuario),
      };
}
