import 'ponto.dart';

/// Painel inicial do Admin (RF21): indicadores da empresa montados a partir das tabelas de
/// ponto, banco de horas, advertências e termômetro de clima. Não tem tabela própria.
class DashboardAdmin {
  const DashboardAdmin({
    required this.referencia,
    required this.resumo,
    required this.ajustes,
    required this.jornadaPorSetor,
    required this.semaforo,
    required this.bancoHoras,
    required this.clima,
    required this.alertas,
  });

  /// "Hoje" para o dashboard: base das datas relativas.
  final DateTime referencia;
  final ResumoAdmin resumo;

  /// Ajustes de ponto que aguardam aprovação (RF10).
  final List<AjustePonto> ajustes;
  final List<JornadaSetor> jornadaPorSetor;
  final DistribuicaoSemaforo semaforo;
  final BancoHorasEmpresa bancoHoras;
  final ClimaDoDia clima;
  final List<AlertaRh> alertas;

  int get ajustesPendentes => ajustes.where((a) => a.situacao == SituacaoAjuste.pendente).length;

  DashboardAdmin copyWith({List<AjustePonto>? ajustes}) => DashboardAdmin(
        referencia: referencia,
        resumo: resumo,
        ajustes: ajustes ?? this.ajustes,
        jornadaPorSetor: jornadaPorSetor,
        semaforo: semaforo,
        bancoHoras: bancoHoras,
        clima: clima,
        alertas: alertas,
      );
}

/// Indicadores do mês: quadro ativo, absenteísmo, horas extras e faltas do ano.
class ResumoAdmin {
  const ResumoAdmin({
    required this.colaboradoresAtivos,
    required this.variacaoAtivosMesAnterior,
    required this.absenteismoPercentual,
    required this.absenteismoMesAnterior,
    required this.horasExtrasMes,
    required this.horasExtrasMesAnterior,
    required this.faltasNoAno,
    required this.tempoMedioDeCasaAnos,
  });

  final int colaboradoresAtivos;
  final int variacaoAtivosMesAnterior;
  final double absenteismoPercentual;
  final double absenteismoMesAnterior;
  final double horasExtrasMes;
  final double horasExtrasMesAnterior;
  final int faltasNoAno;
  final double tempoMedioDeCasaAnos;

  double get variacaoHorasExtrasPercentual =>
      horasExtrasMesAnterior == 0 ? 0 : (horasExtrasMes - horasExtrasMesAnterior) / horasExtrasMesAnterior * 100;
}

/// Atrasos x horas extras do mês, por setor.
class JornadaSetor {
  const JornadaSetor({required this.setor, required this.horasAtraso, required this.horasExtras});

  final String setor;
  final double horasAtraso;
  final double horasExtras;
}

/// Quantos colaboradores estão em cada cor do semáforo de advertências (RN06).
class DistribuicaoSemaforo {
  const DistribuicaoSemaforo({required this.verde, required this.amarelo, required this.vermelho});

  final int verde;
  final int amarelo;
  final int vermelho;

  int get total => verde + amarelo + vermelho;
}

/// Saldo consolidado do banco de horas (BANCO_HORAS).
class BancoHorasEmpresa {
  const BancoHorasEmpresa({
    required this.creditoHoras,
    required this.debitoHoras,
    required this.colaboradoresComSaldoNegativo,
  });

  final double creditoHoras;
  final double debitoHoras;
  final int colaboradoresComSaldoNegativo;
}

enum HumorClima {
  otimo('Ótimo'),
  bem('Bem'),
  neutro('Neutro'),
  cansado('Cansado'),
  estressado('Estressado');

  const HumorClima(this.rotulo);
  final String rotulo;
}

/// Respostas de hoje no termômetro de clima (RF19).
class ClimaDoDia {
  const ClimaDoDia({required this.respostas, required this.publico});

  final Map<HumorClima, int> respostas;
  final int publico;

  int get totalRespostas => respostas.values.fold(0, (a, b) => a + b);
}

enum TipoAlerta { horasExtras, clima, pontoOffline, regua }

/// Alertas automáticos para o RH (RN01, RN05, RN07 e RN08).
class AlertaRh {
  const AlertaRh({required this.tipo, required this.titulo, required this.descricao, required this.momento});

  final TipoAlerta tipo;
  final String titulo;
  final String descricao;
  final DateTime momento;
}
