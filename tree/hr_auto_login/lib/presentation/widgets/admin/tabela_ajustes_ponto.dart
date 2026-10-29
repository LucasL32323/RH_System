import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/ponto.dart';
import 'componentes_admin.dart';

/// "Ajustes de ponto pendentes" (RF10): batida registrada x horário pedido, justificativa
/// e aprovar/recusar na linha. O "Desfazer" só existe no protótipo: a API não desfaz decisão.
class TabelaAjustesPonto extends StatelessWidget {
  const TabelaAjustesPonto({
    super.key,
    required this.ajustes,
    required this.referencia,
    required this.onDecidir,
    this.permiteDesfazer = true,
  });

  final List<AjustePonto> ajustes;
  final DateTime referencia;
  final void Function(AjustePonto ajuste, SituacaoAjuste situacao) onDecidir;

  /// false com a API real (decisão definitiva): esconde o botão "Desfazer".
  final bool permiteDesfazer;

  static const _colunas = [
    ColunaTabela('Colaborador', flex: 26),
    ColunaTabela('Batida', flex: 20),
    ColunaTabela('Horário', flex: 16),
    ColunaTabela('Justificativa', flex: 24),
    ColunaTabela('Ação', flex: 20, alinhamento: Alignment.centerRight),
  ];

  @override
  Widget build(BuildContext context) {
    final pendentes = ajustes.where((a) => a.situacao == SituacaoAjuste.pendente).length;

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Ajustes de ponto pendentes',
            subtitulo: 'Correções de batida pedidas pelos colaboradores, com justificativa',
            contagem: pendentes,
          ),
          const SizedBox(height: 20),
          TabelaAdmin(
            colunas: _colunas,
            larguraMinima: 880,
            mensagemVazia: 'Nenhum ajuste aguardando aprovação.',
            linhas: [
              for (final a in ajustes)
                [
                  CelulaColaborador(colaborador: a.colaborador),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(a.tipoBatida.rotulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto)),
                      const SizedBox(height: 2),
                      Text(
                        '${Formatadores.data(a.data)} · enviado ${Formatadores.momentoRelativo(a.enviadoEm, referencia).toLowerCase()}',
                        style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                      ),
                    ],
                  ),
                  // Horário pedido em destaque; o registrado (riscado) logo abaixo.
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        a.horarioSolicitado,
                        style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto, fontFeatures: numerosTabulares),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        a.horarioRegistrado == null ? 'sem registro' : 'era ${a.horarioRegistrado}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                      ),
                    ],
                  ),
                  Text(
                    a.justificativa,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 13, height: 1.4, color: AppColors.textoSuave),
                  ),
                  FittedBox(
                    fit: BoxFit.scaleDown,
                    alignment: Alignment.centerRight,
                    child: _Acoes(ajuste: a, onDecidir: onDecidir, permiteDesfazer: permiteDesfazer),
                  ),
                ],
            ],
          ),
        ],
      ),
    );
  }
}

class _Acoes extends StatelessWidget {
  const _Acoes({required this.ajuste, required this.onDecidir, required this.permiteDesfazer});

  final AjustePonto ajuste;
  final void Function(AjustePonto ajuste, SituacaoAjuste situacao) onDecidir;
  final bool permiteDesfazer;

  @override
  Widget build(BuildContext context) {
    if (ajuste.situacao != SituacaoAjuste.pendente) {
      final aprovado = ajuste.situacao == SituacaoAjuste.aprovado;
      return Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          EtiquetaStatus(texto: aprovado ? 'Aprovado' : 'Recusado', tom: aprovado ? TomStatus.sucesso : TomStatus.erro),
          if (permiteDesfazer) ...[
            const SizedBox(width: 4),
            TextButton(
              onPressed: () => onDecidir(ajuste, SituacaoAjuste.pendente),
              style: TextButton.styleFrom(
                minimumSize: const Size(0, 32),
                padding: const EdgeInsets.symmetric(horizontal: 8),
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
              child: const Text('Desfazer'),
            ),
          ],
        ],
      );
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        IconButton.outlined(
          onPressed: () => onDecidir(ajuste, SituacaoAjuste.recusado),
          tooltip: 'Recusar ajuste de ${ajuste.colaborador.nome}',
          constraints: const BoxConstraints.tightFor(width: 36, height: 36),
          padding: EdgeInsets.zero,
          style: IconButton.styleFrom(
            foregroundColor: AppColors.erro,
            backgroundColor: AppColors.superficie,
            side: const BorderSide(color: AppColors.borda),
            hoverColor: AppColors.erroFundo,
            shape: const RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(AppTheme.raioCampo))),
          ),
          icon: const Icon(Icons.close_rounded, size: 18),
        ),
        const SizedBox(width: 8),
        BotaoAdmin(
          rotulo: 'Aprovar',
          icone: Icons.check_rounded,
          variante: VarianteBotao.primario,
          compacto: true,
          onPressed: () => onDecidir(ajuste, SituacaoAjuste.aprovado),
        ),
      ],
    );
  }
}
