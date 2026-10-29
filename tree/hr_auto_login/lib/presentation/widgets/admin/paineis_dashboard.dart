import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/dashboard_admin.dart';
import 'componentes_admin.dart';
import 'pagina_admin.dart';

// Painéis do Dashboard do Admin: atrasos x horas extras por setor, semáforo de advertências,
// termômetro de clima, banco de horas e alertas.

/// Cores das duas séries do gráfico de jornada, tiradas da paleta do Figma.
/// Validadas para daltonismo (ΔE 21,9) e contraste >= 3:1; por serem tons pouco saturados,
/// o gráfico também mostra legenda e o valor escrito em cada barra.
const Color _corHorasExtras = AppColors.primaria; // #0D5C63
const Color _corAtrasos = AppColors.destaque; // #44A1A0

/// Atrasos x horas extras do mês, por setor: barras horizontais agrupadas, um eixo só (horas).
class PainelJornadaSetores extends StatelessWidget {
  const PainelJornadaSetores({super.key, required this.setores});

  final List<JornadaSetor> setores;

  @override
  Widget build(BuildContext context) {
    final maximo = setores.fold<double>(1, (m, s) => math.max(m, math.max(s.horasExtras, s.horasAtraso)));
    final totalExtras = setores.fold<double>(0, (t, s) => t + s.horasExtras);
    final totalAtrasos = setores.fold<double>(0, (t, s) => t + s.horasAtraso);

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Atrasos x horas extras',
            subtitulo: 'Horas no mês, por setor · ${Formatadores.horas(totalExtras)} extras e '
                '${Formatadores.horas(totalAtrasos)} de atraso',
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(24, 16, 24, 0),
            child: Wrap(
              spacing: 20,
              runSpacing: 8,
              children: [
                _Legenda(cor: _corHorasExtras, texto: 'Horas extras'),
                _Legenda(cor: _corAtrasos, texto: 'Atrasos'),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
            child: Column(
              children: [
                for (final s in setores)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 112,
                          child: Text(
                            s.setor,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 14, color: AppColors.texto),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            children: [
                              _BarraValor(
                                fracao: s.horasExtras / maximo,
                                cor: _corHorasExtras,
                                rotulo: Formatadores.horas(s.horasExtras),
                                dica: '${s.setor} · horas extras: ${Formatadores.horas(s.horasExtras)}',
                              ),
                              const SizedBox(height: 2), // espaço de 2 px entre as barras do grupo
                              _BarraValor(
                                fracao: s.horasAtraso / maximo,
                                cor: _corAtrasos,
                                rotulo: Formatadores.horas(s.horasAtraso),
                                dica: '${s.setor} · atrasos: ${Formatadores.horas(s.horasAtraso)}',
                              ),
                            ],
                          ),
                        ),
                      ],
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

class _BarraValor extends StatelessWidget {
  const _BarraValor({required this.fracao, required this.cor, required this.rotulo, required this.dica});

  final double fracao;
  final Color cor;
  final String rotulo;
  final String dica;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: dica,
      child: LayoutBuilder(
        builder: (context, limites) {
          const larguraRotulo = 64.0;
          final largura = math.max(4.0, (limites.maxWidth - larguraRotulo) * fracao.clamp(0, 1));
          return SizedBox(
            height: 14,
            child: Row(
              children: [
                Container(
                  width: largura,
                  decoration: BoxDecoration(
                    color: cor,
                    borderRadius: const BorderRadius.horizontal(right: Radius.circular(4)),
                  ),
                ),
                const SizedBox(width: 6),
                Flexible(
                  child: Text(
                    rotulo,
                    maxLines: 1,
                    softWrap: false,
                    overflow: TextOverflow.clip,
                    style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _Legenda extends StatelessWidget {
  const _Legenda({required this.cor, required this.texto});

  final Color cor;
  final String texto;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(width: 12, height: 12, decoration: BoxDecoration(color: cor, borderRadius: BorderRadius.circular(3))),
        const SizedBox(width: 8),
        Text(texto, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
      ],
    );
  }
}

/// Semáforo de advertências da empresa (RN06): barra empilhada + linhas com ícone, rótulo e contagem.
class PainelSemaforo extends StatelessWidget {
  const PainelSemaforo({super.key, required this.semaforo, this.onAbrir});

  final DistribuicaoSemaforo semaforo;
  final VoidCallback? onAbrir;

  @override
  Widget build(BuildContext context) {
    final total = math.max(1, semaforo.total);
    final faixas = [
      (TomStatus.sucesso, Icons.check_circle_rounded, 'Verde', 'sem advertências ativas', semaforo.verde),
      (TomStatus.aviso, Icons.error_rounded, 'Amarelo', '1 ou 2 advertências', semaforo.amarelo),
      (TomStatus.erro, Icons.cancel_rounded, 'Vermelho', '3 ou mais', semaforo.vermelho),
    ];

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Semáforo de advertências',
            subtitulo: 'Colaboradores ativos por situação disciplinar',
            acao: onAbrir == null
                ? null
                : BotaoAdmin(
                    rotulo: 'Gerir',
                    iconeFinal: Icons.chevron_right_rounded,
                    variante: VarianteBotao.texto,
                    compacto: true,
                    onPressed: onAbrir,
                  ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 20, 24, 0),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: SizedBox(
                height: 12,
                child: Row(
                  children: [
                    for (var i = 0; i < faixas.length; i++)
                      if (faixas[i].$5 > 0) ...[
                        if (i > 0) const SizedBox(width: 2),
                        Expanded(flex: math.max(1, faixas[i].$5 * 1000 ~/ total), child: ColoredBox(color: _corStatus(faixas[i].$1))),
                      ],
                  ],
                ),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 12, 24, 20),
            child: Column(
              children: [
                for (final f in faixas)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 6),
                    child: Row(
                      children: [
                        Icon(f.$2, size: 18, color: _corStatus(f.$1)),
                        const SizedBox(width: 10),
                        Text(f.$3, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            f.$4,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 12, color: AppColors.textoSuave),
                          ),
                        ),
                        Text(
                          '${f.$5}',
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            color: AppColors.texto,
                            fontFeatures: numerosTabulares,
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static Color _corStatus(TomStatus tom) => switch (tom) {
        TomStatus.sucesso => AppColors.sucesso,
        TomStatus.aviso => AppColors.alerta,
        TomStatus.erro => AppColors.erro,
        _ => AppColors.textoSuave,
      };
}

/// Termômetro de clima de hoje (RF19): uma série, barras em um único tom.
class PainelClima extends StatelessWidget {
  const PainelClima({super.key, required this.clima});

  final ClimaDoDia clima;

  @override
  Widget build(BuildContext context) {
    final total = math.max(1, clima.totalRespostas);
    final adesao = clima.publico == 0 ? 0.0 : clima.totalRespostas / clima.publico;

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Termômetro de clima',
            subtitulo: 'Hoje · ${clima.totalRespostas} de ${clima.publico} responderam '
                '(${Formatadores.percentual(adesao * 100, casas: 0)})',
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 20),
            child: Column(
              children: [
                for (final humor in HumorClima.values)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 6),
                    child: Tooltip(
                      message: '${humor.rotulo}: ${clima.respostas[humor] ?? 0} respostas',
                      child: Row(
                        children: [
                          SizedBox(
                            width: 84,
                            child: Text(humor.rotulo, style: const TextStyle(fontSize: 14, color: AppColors.texto)),
                          ),
                          Expanded(child: BarraProgresso(fracao: (clima.respostas[humor] ?? 0) / total)),
                          SizedBox(
                            width: 44,
                            child: Text(
                              Formatadores.percentual((clima.respostas[humor] ?? 0) / total * 100, casas: 0),
                              textAlign: TextAlign.right,
                              style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                            ),
                          ),
                        ],
                      ),
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

/// Banco de horas consolidado e faltas do ano.
class PainelBancoHoras extends StatelessWidget {
  const PainelBancoHoras({super.key, required this.banco, required this.faltasNoAno});

  final BancoHorasEmpresa banco;
  final int faltasNoAno;

  @override
  Widget build(BuildContext context) {
    Widget numero(String rotulo, String valor, String nota) => Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(rotulo, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
              const SizedBox(height: 6),
              Text(
                valor,
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                  color: AppColors.texto,
                  fontFeatures: numerosTabulares,
                ),
              ),
              const SizedBox(height: 2),
              Text(nota, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
            ],
          ),
        );

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(titulo: 'Banco de horas', subtitulo: 'Saldo consolidado da empresa'),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                numero('Créditos', Formatadores.horas(banco.creditoHoras), 'a compensar'),
                numero('Débitos', Formatadores.horas(banco.debitoHoras), '${banco.colaboradoresComSaldoNegativo} com saldo negativo'),
                numero('Faltas no ano', '$faltasNoAno', 'desde janeiro'),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Alertas automáticos para o RH (RN01, RN05, RN07, RN08).
class PainelAlertas extends StatelessWidget {
  const PainelAlertas({super.key, required this.alertas, required this.referencia});

  final List<AlertaRh> alertas;
  final DateTime referencia;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(titulo: 'Alertas', subtitulo: 'Gerados pelas regras do sistema', contagem: alertas.length),
          const SizedBox(height: 12),
          ListaAlertas(alertas: alertas, referencia: referencia),
          const SizedBox(height: 12),
        ],
      ),
    );
  }
}

/// Itens de alerta (usada no painel e no diálogo do sino da barra superior).
class ListaAlertas extends StatelessWidget {
  const ListaAlertas({super.key, required this.alertas, required this.referencia});

  final List<AlertaRh> alertas;
  final DateTime referencia;

  static (IconData, TomStatus) visual(TipoAlerta tipo) => switch (tipo) {
        TipoAlerta.horasExtras => (Icons.more_time_rounded, TomStatus.aviso),
        TipoAlerta.clima => (Icons.sentiment_dissatisfied_rounded, TomStatus.erro),
        TipoAlerta.pontoOffline => (Icons.cloud_sync_rounded, TomStatus.info),
        TipoAlerta.regua => (Icons.gavel_rounded, TomStatus.erro),
      };

  @override
  Widget build(BuildContext context) {
    if (alertas.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(24),
        child: Text('Nenhum alerta no momento.', style: TextStyle(fontSize: 14, color: AppColors.textoSuave)),
      );
    }
    return Column(
      children: [
        for (final a in alertas)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: visual(a.tipo).$2.fundo,
                    borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                  ),
                  child: Icon(visual(a.tipo).$1, size: 18, color: visual(a.tipo).$2.texto),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(a.titulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                      const SizedBox(height: 2),
                      Text(a.descricao, style: const TextStyle(fontSize: 13, height: 1.4, color: AppColors.textoSuave)),
                      const SizedBox(height: 4),
                      Text(
                        Formatadores.momentoRelativo(a.momento, referencia),
                        style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
