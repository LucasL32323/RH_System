import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/validation/validadores.dart';
import '../../../domain/entities/advertencia.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../viewmodels/advertencias_view_model.dart';
import '../../widgets/admin/cartao_indicador.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Gestão de advertências (RF13, RF14, RN06, RN07): indicadores, situação de cada colaborador
/// no semáforo, motivos com gravidade e a régua de consequências, com aprovação do RH.
class AbaAdvertencias extends ConsumerWidget {
  const AbaAdvertencias({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  Future<void> _registrar(BuildContext context) async {
    final suspensao = await showDialog<_ResultadoRegistro>(
      context: context,
      builder: (_) => _DialogoAdvertencia(registradaPor: usuario.nome),
    );
    if (suspensao == null || !context.mounted) return;
    avisarAdmin(
      context,
      suspensao.suspensao == null
          ? 'Advertência registrada para ${suspensao.nome}.'
          : 'Régua de consequências: suspensão de ${suspensao.nome} aguardando aprovação do RH.',
    );
  }

  Future<void> _aprovar(BuildContext context, WidgetRef ref, Advertencia s) async {
    try {
      await ref.read(advertenciasViewModelProvider.notifier).aprovarSuspensao(s.id);
      if (context.mounted) avisarAdmin(context, 'Suspensão de ${s.colaborador.nome} aprovada.');
    } on Falha catch (e) {
      if (context.mounted) avisarAdmin(context, e.mensagem);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(advertenciasViewModelProvider);

    return estado.when(
      loading: () => const EstadoCarregando(texto: 'Carregando advertências…'),
      error: (_, __) => Padding(
        padding: const EdgeInsets.all(16),
        child: EstadoErro(
          titulo: 'Não foi possível carregar as advertências',
          onTentar: () => ref.invalidate(advertenciasViewModelProvider),
        ),
      ),
      data: (todas) {
        final situacoes = SituacaoDisciplinar.agrupar(todas);
        final ativas = todas.where((a) => a.ativa && a.medida == Medida.advertencia).length;
        final amarelo = situacoes.where((s) => s.semaforo == Semaforo.amarelo).length;
        final vermelho = situacoes.where((s) => s.semaforo == Semaforo.vermelho).length;
        final aguardando = todas.where((a) => a.aguardandoAprovacao).toList();

        return PaginaAdmin(
          titulo: 'Gestão de advertências',
          subtitulo: 'Semáforo, motivos por gravidade e régua de consequências',
          acoes: [
            BotaoAdmin(
              rotulo: 'Registrar advertência',
              icone: Icons.add_rounded,
              variante: VarianteBotao.primario,
              onPressed: () => _registrar(context),
            ),
          ],
          children: [
            GradeResponsiva(itens: [
              CartaoIndicador(
                rotulo: 'Advertências ativas',
                valor: '$ativas',
                icone: Icons.gavel_rounded,
                variacao: '${situacoes.length}',
                iconeVariacao: Icons.person_rounded,
                tom: TomStatus.neutro,
                nota: 'colaboradores com registro',
              ),
              CartaoIndicador(
                rotulo: 'No amarelo',
                valor: '$amarelo',
                icone: Icons.error_rounded,
                variacao: '1 ou 2',
                iconeVariacao: Icons.gavel_rounded,
                tom: TomStatus.aviso,
                nota: 'advertências ativas',
              ),
              CartaoIndicador(
                rotulo: 'No vermelho',
                valor: '$vermelho',
                icone: Icons.cancel_rounded,
                variacao: '3+',
                iconeVariacao: Icons.gavel_rounded,
                tom: TomStatus.erro,
                nota: 'advertências ou suspensões',
              ),
              CartaoIndicador(
                rotulo: 'Suspensões para aprovar',
                valor: '${aguardando.length}',
                icone: Icons.pending_actions_rounded,
                variacao: 'RH',
                iconeVariacao: Icons.verified_user_rounded,
                tom: aguardando.isEmpty ? TomStatus.neutro : TomStatus.aviso,
                nota: 'aprovação obrigatória',
              ),
            ]),
            if (aguardando.isNotEmpty) ...[
              const SizedBox(height: 24),
              _SuspensoesPendentes(suspensoes: aguardando, onAprovar: (s) => _aprovar(context, ref, s)),
            ],
            const SizedBox(height: 24),
            DuasColunas(
              esquerda: _TabelaSituacoes(situacoes: situacoes),
              direita: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const _Regua(),
                  const SizedBox(height: 24),
                  _Motivos(advertencias: todas),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

/// Cor do semáforo nos ícones (o texto ao lado leva o nome da cor).
Color corSemaforo(Semaforo s) => switch (s) {
      Semaforo.verde => AppColors.sucesso,
      Semaforo.amarelo => AppColors.alerta,
      Semaforo.vermelho => AppColors.erro,
    };

TomStatus tomSemaforo(Semaforo s) => switch (s) {
      Semaforo.verde => TomStatus.sucesso,
      Semaforo.amarelo => TomStatus.aviso,
      Semaforo.vermelho => TomStatus.erro,
    };

TomStatus tomGravidade(Gravidade g) => switch (g) {
      Gravidade.leve => TomStatus.info,
      Gravidade.media => TomStatus.aviso,
      Gravidade.grave => TomStatus.erro,
    };

class _SuspensoesPendentes extends StatelessWidget {
  const _SuspensoesPendentes({required this.suspensoes, required this.onAprovar});

  final List<Advertencia> suspensoes;
  final ValueChanged<Advertencia> onAprovar;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Suspensões aguardando aprovação',
            subtitulo: 'Geradas pela régua de consequências; só valem depois da aprovação do RH (RN07)',
            contagem: suspensoes.length,
          ),
          const SizedBox(height: 12),
          for (final s in suspensoes)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
              child: Row(
                children: [
                  Expanded(child: CelulaColaborador(colaborador: s.colaborador)),
                  const SizedBox(width: 12),
                  BotaoAdmin(
                    rotulo: 'Aprovar suspensão',
                    icone: Icons.check_rounded,
                    variante: VarianteBotao.primario,
                    compacto: true,
                    onPressed: () => onAprovar(s),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _TabelaSituacoes extends StatelessWidget {
  const _TabelaSituacoes({required this.situacoes});

  final List<SituacaoDisciplinar> situacoes;

  static const _colunas = [
    ColunaTabela('Colaborador', flex: 28),
    ColunaTabela('Semáforo', flex: 14),
    ColunaTabela('Ativas', flex: 9),
    ColunaTabela('Último registro', flex: 26),
    ColunaTabela('Régua', flex: 23),
  ];

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Colaboradores com advertências',
            subtitulo: 'Verde sem advertências ativas, amarelo com 1 ou 2 e vermelho com 3 ou mais',
            contagem: situacoes.length,
          ),
          const SizedBox(height: 20),
          TabelaAdmin(
            colunas: _colunas,
            larguraMinima: 860,
            mensagemVazia: 'Nenhuma advertência ativa.',
            linhas: [
              for (final s in situacoes)
                [
                  CelulaColaborador(colaborador: s.colaborador),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.circle, size: 12, color: corSemaforo(s.semaforo)),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(s.semaforo.rotulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto)),
                      ),
                    ],
                  ),
                  Text(
                    '${s.ativas + s.suspensoes}',
                    style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        s.medidas.first.medida == Medida.suspensao ? 'Suspensão' : s.medidas.first.motivo.nome,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 14, color: AppColors.texto),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        '${Formatadores.data(s.medidas.first.data)} · ${s.medidas.first.motivo.gravidade.rotulo.toLowerCase()}',
                        style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                      ),
                    ],
                  ),
                  EtiquetaStatus(texto: s.proximaEtapa, tom: tomSemaforo(s.semaforo)),
                ],
            ],
          ),
        ],
      ),
    );
  }
}

class _Regua extends StatelessWidget {
  const _Regua();

  @override
  Widget build(BuildContext context) {
    const etapas = [
      (Icons.gavel_rounded, '3 advertências leves', 'geram 1 suspensão'),
      (Icons.block_rounded, '2 suspensões', 'levam à análise de justa causa'),
      (Icons.verified_user_rounded, 'Aprovação do RH', 'obrigatória em cada etapa'),
    ];

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(titulo: 'Régua de consequências', subtitulo: 'Aplicada automaticamente pelo sistema'),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
            child: Column(
              children: [
                for (var i = 0; i < etapas.length; i++)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Column(
                        children: [
                          Container(
                            width: 36,
                            height: 36,
                            decoration: BoxDecoration(
                              color: i == 1 ? AppColors.erroFundo : AppColors.infoFundo,
                              borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                            ),
                            child: Icon(etapas[i].$1, size: 18, color: i == 1 ? AppColors.erro : AppColors.primaria),
                          ),
                          if (i < etapas.length - 1) Container(width: 2, height: 20, color: AppColors.borda),
                        ],
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Padding(
                          padding: const EdgeInsets.only(top: 2),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(etapas[i].$2, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                              Text(etapas[i].$3, style: const TextStyle(fontSize: 13, color: AppColors.textoSuave)),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Motivos extends StatelessWidget {
  const _Motivos({required this.advertencias});

  final List<Advertencia> advertencias;

  @override
  Widget build(BuildContext context) {
    final contagem = <MotivoAdvertencia, int>{};
    for (final a in advertencias.where((a) => a.ativa && a.medida == Medida.advertencia)) {
      contagem[a.motivo] = (contagem[a.motivo] ?? 0) + 1;
    }

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(titulo: 'Motivos e gravidade', subtitulo: 'Advertências ativas por motivo'),
          const SizedBox(height: 12),
          for (final m in MotivoAdvertencia.padrao)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 10),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
              child: Row(
                children: [
                  Expanded(child: Text(m.nome, style: const TextStyle(fontSize: 14, color: AppColors.texto))),
                  EtiquetaStatus(texto: m.gravidade.rotulo, tom: tomGravidade(m.gravidade), ponto: false),
                  SizedBox(
                    width: 36,
                    child: Text(
                      '${contagem[m] ?? 0}',
                      textAlign: TextAlign.right,
                      style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto, fontFeatures: numerosTabulares),
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 8),
        ],
      ),
    );
  }
}

class _ResultadoRegistro {
  const _ResultadoRegistro(this.nome, this.suspensao);

  final String nome;
  final Advertencia? suspensao;
}

class _DialogoAdvertencia extends ConsumerStatefulWidget {
  const _DialogoAdvertencia({required this.registradaPor});

  final String registradaPor;

  @override
  ConsumerState<_DialogoAdvertencia> createState() => _DialogoAdvertenciaState();
}

class _DialogoAdvertenciaState extends ConsumerState<_DialogoAdvertencia> {
  final _form = GlobalKey<FormState>();
  final _codigo = TextEditingController();
  final _descricao = TextEditingController();
  MotivoAdvertencia? _motivo;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _codigo.dispose();
    _descricao.dispose();
    super.dispose();
  }

  /// "142", "0142" ou "FUNC-0142" -> "FUNC-0142"
  String _codigoNormalizado() {
    final digitos = Formatadores.digitos(_codigo.text);
    return digitos.isEmpty ? _codigo.text.trim() : 'FUNC-${digitos.padLeft(4, '0')}';
  }

  Future<void> _enviar() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final codigo = _codigoNormalizado();
      final suspensao = await ref.read(advertenciasViewModelProvider.notifier).registrar(
            codigoFuncionario: codigo,
            motivo: _motivo!,
            descricao: _descricao.text.trim(),
            registradaPor: widget.registradaPor,
          );
      final nome = ref
              .read(advertenciasViewModelProvider)
              .valueOrNull
              ?.lastWhere((a) => a.colaborador.matricula == codigo)
              .colaborador
              .nome ??
          codigo;
      if (mounted) Navigator.of(context).pop(_ResultadoRegistro(nome, suspensao));
    } on Falha catch (e) {
      setState(() => _erro = e.mensagem);
    } catch (_) {
      setState(() => _erro = 'Não foi possível registrar. Tente novamente.');
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DialogoAdmin(
      titulo: 'Registrar advertência',
      subtitulo: 'Ao completar 3 advertências leves, a régua gera uma suspensão para o RH aprovar.',
      rotuloConfirmar: 'Registrar',
      enviando: _enviando,
      onConfirmar: _enviar,
      conteudo: Form(
        key: _form,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_erro != null) ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: AppColors.erroFundo, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
                child: Text(_erro!, style: const TextStyle(fontSize: 14, color: AppColors.erro)),
              ),
              const SizedBox(height: 16),
            ],
            CampoAdmin(
              rotulo: 'Código do funcionário',
              controller: _codigo,
              dica: 'FUNC-0142 ou 142',
              validator: (v) => Validadores.obrigatorio(v, 'Informe o código FUNC.'),
            ),
            const SizedBox(height: 16),
            SelecaoAdmin<MotivoAdvertencia>(
              rotulo: 'Motivo',
              valor: _motivo,
              opcoes: MotivoAdvertencia.padrao,
              rotuloDe: (m) => '${m.nome} (${m.gravidade.rotulo.toLowerCase()})',
              onChanged: (m) => setState(() => _motivo = m),
              validator: (m) => m == null ? 'Escolha o motivo.' : null,
            ),
            const SizedBox(height: 16),
            CampoAdmin(
              rotulo: 'Descrição',
              controller: _descricao,
              maxLines: 3,
              textCapitalization: TextCapitalization.sentences,
              validator: (v) => Validadores.obrigatorio(v, 'Descreva o ocorrido.'),
            ),
          ],
        ),
      ),
    );
  }
}
