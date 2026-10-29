import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/formatting/mascara_entrada.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/validation/validadores.dart';
import '../../../domain/entities/ponto.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../viewmodels/ponto_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Registro de ponto (RF07, RF09, RF10): bater o ponto em um toque, ver as batidas do dia,
/// o espelho da semana e pedir ajuste com justificativa.
class AbaRegistroPonto extends ConsumerWidget {
  const AbaRegistroPonto({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  Future<void> _bater(BuildContext context, WidgetRef ref) async {
    try {
      final batida = await ref.read(pontoViewModelProvider.notifier).registrarBatida();
      if (batida == null || !context.mounted) return;
      avisarAdmin(context, '${batida.tipo.rotulo} registrada às ${Formatadores.hora(batida.horario)}.');
    } on Falha catch (e) {
      if (context.mounted) avisarAdmin(context, e.mensagem);
    } catch (_) {
      if (context.mounted) avisarAdmin(context, 'Não foi possível registrar a batida. Tente novamente.');
    }
  }

  Future<void> _pedirAjuste(BuildContext context, EspelhoPonto espelho) async {
    final enviado = await showDialog<bool>(
      context: context,
      builder: (_) => _DialogoAjuste(espelho: espelho),
    );
    if (enviado == true && context.mounted) {
      avisarAdmin(context, 'Pedido de ajuste enviado ao seu gestor.');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(pontoViewModelProvider);

    return estado.when(
      loading: () => const EstadoCarregando(texto: 'Carregando o espelho de ponto…'),
      error: (_, __) => Padding(
        padding: const EdgeInsets.all(16),
        child: EstadoErro(
          titulo: 'Não foi possível carregar o ponto',
          onTentar: () => ref.invalidate(pontoViewModelProvider),
        ),
      ),
      data: (espelho) => PaginaAdmin(
        titulo: 'Registro de ponto',
        subtitulo: '${usuario.nome} · ${usuario.codigo} · jornada de 8 h por dia',
        acoes: [
          BotaoAdmin(
            rotulo: 'Solicitar ajuste',
            icone: Icons.edit_calendar_rounded,
            onPressed: () => _pedirAjuste(context, espelho),
          ),
        ],
        children: [
          DuasColunas(
            flexEsquerda: 2,
            flexDireita: 3,
            esquerda: _PainelBatida(espelho: espelho, onBater: () => _bater(context, ref)),
            direita: _EspelhoSemana(espelho: espelho),
          ),
          if (espelho.minhasSolicitacoes.isNotEmpty) ...[
            const SizedBox(height: 24),
            _MinhasSolicitacoes(ajustes: espelho.minhasSolicitacoes, referencia: espelho.agora),
          ],
        ],
      ),
    );
  }
}

class _PainelBatida extends StatelessWidget {
  const _PainelBatida({required this.espelho, required this.onBater});

  final EspelhoPonto espelho;
  final VoidCallback onBater;

  @override
  Widget build(BuildContext context) {
    final proxima = espelho.proximaBatida;
    final hoje = espelho.hoje;

    return PainelAdmin(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            Formatadores.dataPorExtenso(espelho.agora),
            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.textoSuave),
          ),
          const SizedBox(height: 8),
          Text(
            Formatadores.hora(espelho.agora),
            style: const TextStyle(
              fontSize: 48,
              height: 1,
              fontWeight: FontWeight.w700,
              letterSpacing: -1,
              color: AppColors.texto,
              fontFeatures: numerosTabulares,
            ),
          ),
          const SizedBox(height: 6),
          const Row(
            children: [
              Icon(Icons.dns_rounded, size: 14, color: AppColors.textoSuave),
              SizedBox(width: 6),
              Flexible(
                child: Text(
                  'O horário da batida é o do servidor, no momento do toque.',
                  style: TextStyle(fontSize: 12, color: AppColors.textoSuave),
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
          SizedBox(
            height: 56,
            child: FilledButton.icon(
              key: const Key('botao_bater_ponto'),
              onPressed: proxima == null ? null : onBater,
              icon: Icon(proxima == null ? Icons.check_circle_rounded : Icons.fingerprint_rounded),
              label: Text(proxima == null ? 'Jornada de hoje encerrada' : 'Registrar ${proxima.rotulo.toLowerCase()}'),
            ),
          ),
          const SizedBox(height: 24),
          const Text('Batidas de hoje', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
          const SizedBox(height: 8),
          for (final tipo in TipoBatida.values)
            Container(
              padding: const EdgeInsets.symmetric(vertical: 10),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
              child: Row(
                children: [
                  Icon(
                    hoje?.batida(tipo) != null
                        ? Icons.check_circle_rounded
                        : tipo == proxima
                            ? Icons.radio_button_checked_rounded
                            : Icons.radio_button_unchecked_rounded,
                    size: 18,
                    color: hoje?.batida(tipo) != null
                        ? AppColors.sucesso
                        : tipo == proxima
                            ? AppColors.primaria
                            : AppColors.borda,
                  ),
                  const SizedBox(width: 10),
                  Expanded(child: Text(tipo.rotulo, style: const TextStyle(fontSize: 14, color: AppColors.texto))),
                  Text(
                    hoje?.batida(tipo) == null ? '--:--' : Formatadores.hora(hoje!.batida(tipo)!.horario),
                    style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto, fontFeatures: numerosTabulares),
                  ),
                ],
              ),
            ),
          Container(
            padding: const EdgeInsets.only(top: 12),
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
            child: Row(
              children: [
                const Expanded(child: Text('Trabalhado hoje', style: TextStyle(fontSize: 14, color: AppColors.textoSuave))),
                Text(
                  Formatadores.duracao(hoje?.trabalhado ?? Duration.zero),
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto, fontFeatures: numerosTabulares),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _EspelhoSemana extends StatelessWidget {
  const _EspelhoSemana({required this.espelho});

  final EspelhoPonto espelho;

  static const _colunas = [
    ColunaTabela('Dia', flex: 16),
    ColunaTabela('Entrada', flex: 12),
    ColunaTabela('Intervalo', flex: 12),
    ColunaTabela('Retorno', flex: 12),
    ColunaTabela('Saída', flex: 12),
    ColunaTabela('Trabalhado', flex: 14),
    ColunaTabela('Saldo', flex: 12, alinhamento: Alignment.centerRight),
  ];

  static const _diasCurtos = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];

  @override
  Widget build(BuildContext context) {
    final saldo = espelho.saldoBancoHoras;
    Widget hora(DiaEspelho d, TipoBatida t) => Text(
          d.batida(t) == null ? '--:--' : Formatadores.hora(d.batida(t)!.horario),
          style: TextStyle(
            fontSize: 14,
            color: d.batida(t) == null ? AppColors.textoSuave : AppColors.texto,
            fontFeatures: numerosTabulares,
          ),
        );

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(titulo: 'Espelho da semana', subtitulo: 'Horas calculadas pela carga horária do cargo'),
          const SizedBox(height: 20),
          TabelaAdmin(
            colunas: _colunas,
            larguraMinima: 640,
            mensagemVazia: 'Sem batidas nesta semana.',
            linhas: [
              for (final d in espelho.semana)
                [
                  Text(
                    '${_diasCurtos[d.data.weekday - 1]} ${Formatadores.diaMes(d.data)}',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: d == espelho.hoje ? FontWeight.w700 : FontWeight.w500,
                      color: AppColors.texto,
                      fontFeatures: numerosTabulares,
                    ),
                  ),
                  hora(d, TipoBatida.entrada),
                  hora(d, TipoBatida.saidaIntervalo),
                  hora(d, TipoBatida.retornoIntervalo),
                  hora(d, TipoBatida.saida),
                  Text(
                    d.batidas.isEmpty ? '-' : Formatadores.duracao(d.trabalhado),
                    style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
                  ),
                  d.completo
                      ? EtiquetaStatus(
                          texto: Formatadores.duracao(d.saldo, sinal: true),
                          tom: d.saldo.isNegative ? TomStatus.erro : TomStatus.sucesso,
                          ponto: false,
                        )
                      : const Text('-', style: TextStyle(fontSize: 14, color: AppColors.textoSuave)),
                ],
            ],
          ),
          Container(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 16),
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
            child: Row(
              children: [
                const Icon(Icons.account_balance_wallet_rounded, size: 18, color: AppColors.destaque),
                const SizedBox(width: 10),
                const Expanded(
                  child: Text('Saldo do banco de horas', style: TextStyle(fontSize: 14, color: AppColors.textoSuave)),
                ),
                Text(
                  Formatadores.duracao(saldo, sinal: true),
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: saldo.isNegative ? AppColors.erro : AppColors.sucesso,
                    fontFeatures: numerosTabulares,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _MinhasSolicitacoes extends StatelessWidget {
  const _MinhasSolicitacoes({required this.ajustes, required this.referencia});

  final List<AjustePonto> ajustes;
  final DateTime referencia;

  /// Etiqueta conforme a decisão do gestor (a API devolve o status real de cada pedido).
  static (String, TomStatus) _etiqueta(SituacaoAjuste situacao) => switch (situacao) {
        SituacaoAjuste.pendente => ('Aguardando gestor', TomStatus.aviso),
        SituacaoAjuste.aprovado => ('Aprovado', TomStatus.sucesso),
        SituacaoAjuste.recusado => ('Recusado', TomStatus.erro),
      };

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Minhas solicitações de ajuste',
            subtitulo: 'Analisadas pelo gestor do seu setor',
            contagem: ajustes.length,
          ),
          const SizedBox(height: 12),
          for (final a in ajustes)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${a.tipoBatida.rotulo} de ${Formatadores.data(a.data)} → ${a.horarioSolicitado}',
                          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                        ),
                        const SizedBox(height: 2),
                        Text(a.justificativa, style: const TextStyle(fontSize: 13, color: AppColors.textoSuave)),
                        if (a.observacao != null) ...[
                          const SizedBox(height: 6),
                          Text(
                            a.situacao == SituacaoAjuste.recusado
                                ? 'Motivo da recusa: ${a.observacao}'
                                : 'Observação do gestor: ${a.observacao}',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                              color: a.situacao == SituacaoAjuste.recusado ? AppColors.erro : AppColors.texto,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  EtiquetaStatus(texto: _etiqueta(a.situacao).$1, tom: _etiqueta(a.situacao).$2),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _DialogoAjuste extends ConsumerStatefulWidget {
  const _DialogoAjuste({required this.espelho});

  final EspelhoPonto espelho;

  @override
  ConsumerState<_DialogoAjuste> createState() => _DialogoAjusteState();
}

class _DialogoAjusteState extends ConsumerState<_DialogoAjuste> {
  final _form = GlobalKey<FormState>();
  final _horario = TextEditingController();
  final _justificativa = TextEditingController();
  late DiaEspelho? _dia = widget.espelho.hoje ?? (widget.espelho.semana.isEmpty ? null : widget.espelho.semana.first);
  TipoBatida? _tipo = TipoBatida.entrada;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _horario.dispose();
    _justificativa.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await ref.read(pontoViewModelProvider.notifier).solicitarAjuste(
            data: _dia!.data,
            tipoBatida: _tipo!,
            horarioSolicitado: _horario.text.trim(),
            justificativa: _justificativa.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
    } on Falha catch (e) {
      setState(() => _erro = e.mensagem);
    } catch (_) {
      setState(() => _erro = 'Não foi possível enviar. Tente novamente.');
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final dias = widget.espelho.semana.where((d) => !d.data.isAfter(widget.espelho.agora)).toList();

    return DialogoAdmin(
      titulo: 'Solicitar ajuste de ponto',
      subtitulo: 'O pedido vai para o gestor do seu setor. Batidas retroativas só entram com aprovação (RN02).',
      rotuloConfirmar: 'Enviar pedido',
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
                decoration: BoxDecoration(
                  color: AppColors.erroFundo,
                  borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                ),
                child: Text(_erro!, style: const TextStyle(fontSize: 14, color: AppColors.erro)),
              ),
              const SizedBox(height: 16),
            ],
            LinhaCampos(
              larguraMinima: 420,
              campos: [
                SelecaoAdmin<DiaEspelho>(
                  rotulo: 'Dia',
                  valor: dias.contains(_dia) ? _dia : null,
                  opcoes: dias,
                  rotuloDe: (d) => Formatadores.data(d.data),
                  onChanged: (d) => setState(() => _dia = d),
                  validator: (d) => d == null ? 'Escolha o dia.' : null,
                ),
                SelecaoAdmin<TipoBatida>(
                  rotulo: 'Batida',
                  valor: _tipo,
                  opcoes: TipoBatida.values,
                  rotuloDe: (t) => t.rotulo,
                  onChanged: (t) => setState(() => _tipo = t),
                  validator: (t) => t == null ? 'Escolha a batida.' : null,
                ),
              ],
            ),
            const SizedBox(height: 16),
            CampoAdmin(
              rotulo: 'Horário correto',
              controller: _horario,
              dica: 'hh:mm',
              mascara: MascaraEntrada.hora,
              validator: Validadores.horario,
            ),
            const SizedBox(height: 16),
            CampoAdmin(
              rotulo: 'Justificativa',
              controller: _justificativa,
              dica: 'Explique o que aconteceu',
              maxLines: 3,
              textCapitalization: TextCapitalization.sentences,
              validator: (v) {
                final texto = v?.trim() ?? '';
                if (texto.isEmpty) return 'A justificativa é obrigatória.';
                if (texto.length < 10) return 'Descreva com pelo menos 10 caracteres.';
                return null;
              },
            ),
          ],
        ),
      ),
    );
  }
}
