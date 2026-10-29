import 'colaborador_resumo.dart';

enum Gravidade {
  leve('Leve'),
  media('Média'),
  grave('Grave');

  const Gravidade(this.rotulo);
  final String rotulo;
}

/// Motivos cadastrados pelo RH, cada um com a sua gravidade.
class MotivoAdvertencia {
  const MotivoAdvertencia(this.nome, this.gravidade);

  final String nome;
  final Gravidade gravidade;

  static const List<MotivoAdvertencia> padrao = [
    MotivoAdvertencia('Atraso recorrente', Gravidade.leve),
    MotivoAdvertencia('Uso indevido do celular', Gravidade.leve),
    MotivoAdvertencia('Falta injustificada', Gravidade.media),
    MotivoAdvertencia('Descumprimento de norma interna', Gravidade.media),
    MotivoAdvertencia('Não uso de EPI', Gravidade.grave),
    MotivoAdvertencia('Insubordinação', Gravidade.grave),
  ];

  @override
  bool operator ==(Object other) => other is MotivoAdvertencia && other.nome == nome;

  @override
  int get hashCode => nome.hashCode;
}

enum Medida {
  advertencia('Advertência'),
  suspensao('Suspensão');

  const Medida(this.rotulo);
  final String rotulo;
}

/// Registro em ADVERTENCIA. Suspensões geradas pela régua aguardam a aprovação do RH (RN07).
class Advertencia {
  const Advertencia({
    required this.id,
    required this.colaborador,
    required this.data,
    required this.motivo,
    required this.descricao,
    required this.registradaPor,
    this.medida = Medida.advertencia,
    this.aguardandoAprovacao = false,
    this.ativa = true,
  });

  final String id;
  final ColaboradorResumo colaborador;
  final DateTime data;
  final MotivoAdvertencia motivo;
  final String descricao;
  final String registradaPor;
  final Medida medida;
  final bool aguardandoAprovacao;
  final bool ativa;

  Advertencia copyWith({bool? aguardandoAprovacao}) => Advertencia(
        id: id,
        colaborador: colaborador,
        data: data,
        motivo: motivo,
        descricao: descricao,
        registradaPor: registradaPor,
        medida: medida,
        aguardandoAprovacao: aguardandoAprovacao ?? this.aguardandoAprovacao,
        ativa: ativa,
      );
}

/// Semáforo de advertências (RN06): verde sem advertências ativas, amarelo com 1 ou 2, vermelho com 3+.
enum Semaforo {
  verde('Verde', 'Sem advertências ativas'),
  amarelo('Amarelo', '1 ou 2 advertências ativas'),
  vermelho('Vermelho', '3 ou mais advertências ativas');

  const Semaforo(this.rotulo, this.descricao);
  final String rotulo;
  final String descricao;

  static Semaforo de(int ativas) => ativas == 0
      ? verde
      : ativas < 3
          ? amarelo
          : vermelho;
}

/// Régua de consequências (RN07): 3 advertências leves geram 1 suspensão e
/// 2 suspensões levam à análise de justa causa, sempre com aprovação do RH.
class ReguaConsequencias {
  ReguaConsequencias._();

  static const int levesParaSuspensao = 3;
  static const int suspensoesParaJustaCausa = 2;

  /// Situação disciplinar de um colaborador a partir das medidas ativas.
  static String proximaEtapa({required int leves, required int suspensoes}) {
    if (suspensoes >= suspensoesParaJustaCausa) return 'Análise de justa causa';
    if (leves >= levesParaSuspensao) return 'Suspensão';
    final faltam = levesParaSuspensao - leves;
    final alvo = suspensoes == 0 ? 'suspensão' : '2ª suspensão (justa causa)';
    return '$faltam ${faltam == 1 ? 'leve' : 'leves'} para $alvo';
  }
}

/// Resumo disciplinar de um colaborador (linha da tabela de gestão de advertências).
class SituacaoDisciplinar {
  const SituacaoDisciplinar({required this.colaborador, required this.medidas});

  final ColaboradorResumo colaborador;
  final List<Advertencia> medidas; // ativas, mais recente primeiro

  int get ativas => medidas.where((m) => m.medida == Medida.advertencia).length;
  int get leves =>
      medidas.where((m) => m.medida == Medida.advertencia && m.motivo.gravidade == Gravidade.leve).length;
  int get suspensoes => medidas.where((m) => m.medida == Medida.suspensao).length;
  Semaforo get semaforo => Semaforo.de(ativas + suspensoes);

  /// Leves que ainda não viraram suspensão (cada suspensão "consome" 3 leves).
  int get levesSemSuspensao =>
      (leves - suspensoes * ReguaConsequencias.levesParaSuspensao).clamp(0, ReguaConsequencias.levesParaSuspensao);

  bool get suspensaoAguardandoRh => medidas.any((m) => m.medida == Medida.suspensao && m.aguardandoAprovacao);

  String get proximaEtapa {
    if (suspensoes >= ReguaConsequencias.suspensoesParaJustaCausa) return 'Análise de justa causa';
    if (suspensaoAguardandoRh) return 'Suspensão aguardando aprovação do RH';
    return ReguaConsequencias.proximaEtapa(leves: levesSemSuspensao, suspensoes: suspensoes);
  }

  static List<SituacaoDisciplinar> agrupar(List<Advertencia> todas) {
    final porCodigo = <String, List<Advertencia>>{};
    for (final a in todas.where((a) => a.ativa)) {
      porCodigo.putIfAbsent(a.colaborador.matricula, () => []).add(a);
    }
    final lista = [
      for (final medidas in porCodigo.values)
        SituacaoDisciplinar(
          colaborador: medidas.first.colaborador,
          medidas: [...medidas]..sort((a, b) => b.data.compareTo(a.data)),
        ),
    ];
    lista.sort((a, b) {
      final porSemaforo = b.semaforo.index.compareTo(a.semaforo.index);
      return porSemaforo != 0 ? porSemaforo : b.medidas.first.data.compareTo(a.medidas.first.data);
    });
    return lista;
  }
}
