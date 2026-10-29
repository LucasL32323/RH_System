import 'colaborador_resumo.dart';

/// Batidas na ordem obrigatória (RN04): entrada, saída para o intervalo, retorno e saída.
enum TipoBatida {
  entrada('Entrada'),
  saidaIntervalo('Saída para intervalo'),
  retornoIntervalo('Retorno do intervalo'),
  saida('Saída');

  const TipoBatida(this.rotulo);
  final String rotulo;

  /// Próxima batida liberada, ou null depois da saída.
  TipoBatida? get proxima => index < values.length - 1 ? values[index + 1] : null;
}

class Batida {
  const Batida({required this.tipo, required this.horario, this.offline = false});

  final TipoBatida tipo;
  final DateTime horario; // horário do servidor (RF07)

  /// Feita sem internet: mantém o horário do aparelho e vai para conferência do gestor (RN05).
  final bool offline;
}

/// Um dia do espelho de ponto, com as horas calculadas pela carga do cargo (RF11).
class DiaEspelho {
  const DiaEspelho({required this.data, required this.batidas, required this.cargaHoras});

  final DateTime data;
  final List<Batida> batidas;
  final double cargaHoras;

  Batida? batida(TipoBatida tipo) {
    for (final b in batidas) {
      if (b.tipo == tipo) return b;
    }
    return null;
  }

  /// Soma dos períodos fechados (entrada -> intervalo, retorno -> saída).
  Duration get trabalhado {
    Duration periodo(TipoBatida inicio, TipoBatida fim) {
      final a = batida(inicio);
      final b = batida(fim);
      if (a == null || b == null) return Duration.zero;
      return b.horario.difference(a.horario);
    }

    return periodo(TipoBatida.entrada, TipoBatida.saidaIntervalo) +
        periodo(TipoBatida.retornoIntervalo, TipoBatida.saida);
  }

  bool get completo => batida(TipoBatida.saida) != null;

  /// Crédito (positivo) ou débito (negativo) do banco de horas. Zero enquanto o dia não fecha.
  Duration get saldo =>
      completo ? trabalhado - Duration(minutes: (cargaHoras * 60).round()) : Duration.zero;
}

/// Espelho de ponto de quem está logado: semana atual e saldo do banco de horas.
class EspelhoPonto {
  const EspelhoPonto({
    required this.agora,
    required this.semana,
    required this.saldoBancoHoras,
    required this.minhasSolicitacoes,
  });

  final DateTime agora;

  /// Segunda a sexta da semana atual; o dia de hoje inclui as batidas já feitas.
  final List<DiaEspelho> semana;
  final Duration saldoBancoHoras;
  final List<AjustePonto> minhasSolicitacoes;

  DiaEspelho? get hoje {
    for (final d in semana) {
      if (d.data.year == agora.year && d.data.month == agora.month && d.data.day == agora.day) return d;
    }
    return null;
  }

  /// Próxima batida liberada hoje (null quando o dia já fechou).
  TipoBatida? get proximaBatida {
    final batidas = hoje?.batidas ?? const [];
    return batidas.isEmpty ? TipoBatida.entrada : batidas.last.tipo.proxima;
  }
}

enum SituacaoAjuste { pendente, aprovado, recusado }

/// Pedido de correção de uma batida (MANUTENCAO_PONTO), aprovado ou recusado pelo gestor (RF10).
class AjustePonto {
  const AjustePonto({
    required this.id,
    required this.colaborador,
    required this.data,
    required this.tipoBatida,
    required this.horarioSolicitado,
    required this.justificativa,
    required this.enviadoEm,
    this.horarioRegistrado,
    this.situacao = SituacaoAjuste.pendente,
    this.observacao,
  });

  final String id;
  final ColaboradorResumo colaborador;
  final DateTime data;
  final TipoBatida tipoBatida;

  /// "08:47"; null quando a batida não foi registrada.
  final String? horarioRegistrado;
  final String horarioSolicitado;
  final String justificativa;
  final DateTime enviadoEm;
  final SituacaoAjuste situacao;

  /// Observação de quem decidiu (motivo da recusa ou nota da aprovação). Null enquanto pendente
  /// ou quando o gestor não escreveu nada.
  final String? observacao;

  /// Copia trocando a decisão. Voltar para [SituacaoAjuste.pendente] apaga a observação;
  /// nos demais casos, [observacao] nova vence e, sem ela, a anterior é mantida.
  AjustePonto copyWith({SituacaoAjuste? situacao, String? observacao}) {
    final novaSituacao = situacao ?? this.situacao;
    return AjustePonto(
      id: id,
      colaborador: colaborador,
      data: data,
      tipoBatida: tipoBatida,
      horarioRegistrado: horarioRegistrado,
      horarioSolicitado: horarioSolicitado,
      justificativa: justificativa,
      enviadoEm: enviadoEm,
      situacao: novaSituacao,
      observacao: novaSituacao == SituacaoAjuste.pendente ? null : (observacao ?? this.observacao),
    );
  }
}
