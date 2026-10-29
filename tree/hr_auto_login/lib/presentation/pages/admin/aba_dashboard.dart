import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/dashboard_admin.dart';
import '../../../domain/entities/ponto.dart';
import '../../viewmodels/dashboard_admin_view_model.dart';
import '../../widgets/admin/cartao_indicador.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/dialogo_motivo_recusa.dart';
import '../../widgets/admin/menu_icones_admin.dart';
import '../../widgets/admin/pagina_admin.dart';
import '../../widgets/admin/paineis_dashboard.dart';
import '../../widgets/admin/tabela_ajustes_ponto.dart';

/// Dashboard do Admin (RF21): quadro ativo, absenteísmo, horas extras e ajustes pendentes;
/// atrasos x horas extras, semáforo de advertências, termômetro de clima, banco de horas e alertas.
class AbaDashboard extends ConsumerWidget {
  const AbaDashboard({super.key, required this.onAbrir});

  final ValueChanged<AbaAdmin> onAbrir;

  Future<void> _decidir(BuildContext context, WidgetRef ref, AjustePonto ajuste, SituacaoAjuste situacao) async {
    final vm = ref.read(dashboardAdminViewModelProvider.notifier);

    // Recusar exige o motivo (a API pede 5+ caracteres; o colaborador o lê em "Minhas solicitações").
    String? motivo;
    if (situacao == SituacaoAjuste.recusado) {
      motivo = await pedirMotivoRecusa(context, colaborador: ajuste.colaborador.nome);
      if (motivo == null || !context.mounted) return; // cancelou
    }

    final ok = await vm.decidir(ajuste.id, situacao, observacao: motivo);
    if (!context.mounted) return;
    if (!ok) {
      // Mensagem da API quando existe (ex.: pedido já decidido por outra pessoa).
      avisarAdmin(context, vm.ultimoErro ?? 'Não foi possível registrar a decisão. Tente novamente.');
      return;
    }
    if (situacao == SituacaoAjuste.pendente) {
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      return;
    }
    final verbo = situacao == SituacaoAjuste.aprovado ? 'aprovado' : 'recusado';
    avisarAdmin(
      context,
      'Ajuste de ${ajuste.colaborador.nome}: $verbo.',
      // A API não desfaz decisão: só o protótipo oferece "Desfazer".
      acao: AppConfig.modoPrototipo
          ? SnackBarAction(
              label: 'Desfazer',
              textColor: AppColors.apoio,
              onPressed: () => vm.decidir(ajuste.id, SituacaoAjuste.pendente),
            )
          : null,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(dashboardAdminViewModelProvider);

    return estado.when(
      loading: () => const EstadoCarregando(texto: 'Carregando indicadores…'),
      error: (_, __) => Padding(
        padding: const EdgeInsets.all(16),
        child: EstadoErro(
          titulo: 'Não foi possível carregar o dashboard',
          onTentar: () => ref.read(dashboardAdminViewModelProvider.notifier).recarregar(),
        ),
      ),
      data: (d) => PaginaAdmin(
        titulo: 'Dashboard',
        subtitulo: 'Indicadores de ${Formatadores.nomeMes(d.referencia.month)} de ${d.referencia.year} · '
            '${d.resumo.colaboradoresAtivos} colaboradores ativos',
        acoes: [
          BotaoAdmin(rotulo: 'Registrar ponto', icone: Icons.fingerprint_rounded, onPressed: () => onAbrir(AbaAdmin.ponto)),
          BotaoAdmin(
            rotulo: 'Novo funcionário',
            icone: Icons.person_add_alt_1_rounded,
            variante: VarianteBotao.primario,
            onPressed: () => onAbrir(AbaAdmin.novoFuncionario),
          ),
        ],
        children: [
          GradeResponsiva(itens: _indicadores(d)),
          const SizedBox(height: 24),
          DuasColunas(
            esquerda: TabelaAjustesPonto(
              ajustes: d.ajustes,
              referencia: d.referencia,
              onDecidir: (a, s) => _decidir(context, ref, a, s),
              permiteDesfazer: AppConfig.modoPrototipo,
            ),
            direita: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PainelSemaforo(semaforo: d.semaforo, onAbrir: () => onAbrir(AbaAdmin.advertencias)),
                const SizedBox(height: 24),
                PainelClima(clima: d.clima),
              ],
            ),
          ),
          const SizedBox(height: 24),
          DuasColunas(
            flexEsquerda: 3,
            flexDireita: 2,
            esquerda: PainelJornadaSetores(setores: d.jornadaPorSetor),
            direita: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PainelBancoHoras(banco: d.bancoHoras, faltasNoAno: d.resumo.faltasNoAno),
                const SizedBox(height: 24),
                PainelAlertas(alertas: d.alertas, referencia: d.referencia),
              ],
            ),
          ),
        ],
      ),
    );
  }

  List<Widget> _indicadores(DashboardAdmin d) {
    final r = d.resumo;
    final mesAnterior = Formatadores.nomeMes(d.referencia.month == 1 ? 12 : d.referencia.month - 1);
    final variacaoAbsenteismo = r.absenteismoPercentual - r.absenteismoMesAnterior;
    final variacaoExtras = r.variacaoHorasExtrasPercentual;

    final pendentes = d.ajustes.where((a) => a.situacao == SituacaoAjuste.pendente).toList();
    final diasMaisAntigo = pendentes.isEmpty
        ? 0
        : pendentes.map((a) => d.referencia.difference(a.enviadoEm).inDays).reduce((a, b) => a > b ? a : b);

    return [
      CartaoIndicador(
        rotulo: 'Colaboradores ativos',
        valor: '${r.colaboradoresAtivos}',
        icone: Icons.groups_rounded,
        variacao: '${r.variacaoAtivosMesAnterior >= 0 ? '+' : ''}${r.variacaoAtivosMesAnterior}',
        iconeVariacao: r.variacaoAtivosMesAnterior >= 0 ? Icons.north_east_rounded : Icons.south_east_rounded,
        tom: TomStatus.neutro,
        nota: 'vs. $mesAnterior · ${Formatadores.anos(r.tempoMedioDeCasaAnos)} de casa em média',
      ),
      CartaoIndicador(
        rotulo: 'Absenteísmo',
        valor: Formatadores.percentual(r.absenteismoPercentual),
        icone: Icons.event_busy_rounded,
        variacao: '${Formatadores.percentual(variacaoAbsenteismo, sinal: true).replaceAll('%', '')} p.p.',
        iconeVariacao: variacaoAbsenteismo <= 0 ? Icons.south_east_rounded : Icons.north_east_rounded,
        tom: variacaoAbsenteismo <= 0 ? TomStatus.sucesso : TomStatus.aviso,
        nota: 'vs. $mesAnterior',
      ),
      CartaoIndicador(
        rotulo: 'Horas extras no mês',
        valor: Formatadores.horas(r.horasExtrasMes),
        icone: Icons.more_time_rounded,
        variacao: Formatadores.percentual(variacaoExtras, sinal: true),
        iconeVariacao: variacaoExtras >= 0 ? Icons.north_east_rounded : Icons.south_east_rounded,
        tom: variacaoExtras > 0 ? TomStatus.aviso : TomStatus.sucesso,
        nota: 'vs. $mesAnterior',
      ),
      CartaoIndicador(
        rotulo: 'Ajustes pendentes',
        valor: '${pendentes.length}',
        icone: Icons.edit_calendar_rounded,
        variacao: diasMaisAntigo == 1 ? '1 dia' : '$diasMaisAntigo dias',
        iconeVariacao: Icons.schedule_rounded,
        tom: diasMaisAntigo >= 3 ? TomStatus.aviso : TomStatus.neutro,
        nota: pendentes.isEmpty ? 'nada aguardando' : 'o mais antigo aguarda',
      ),
    ];
  }
}
