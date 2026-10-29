import '../entities/ponto.dart';

/// Contrato do registro de ponto de quem está logado (RF07, RF09, RF10, RF11).
/// Duas implementações:
///  - PontoRepositoryApi (data/repositories): API .NET, horário do servidor (RF07);
///  - AdminRepositoryPrototipo: em memória, para validar as telas sem back-end.
abstract class PontoRepository {
  /// Semana atual (segunda a sexta), batidas de hoje e saldo do banco de horas.
  Future<EspelhoPonto> carregarPonto();

  /// Registra a próxima batida da sequência (RN04) com o horário do servidor (RF07).
  Future<Batida> registrarBatida();

  /// Pede a correção de uma batida ao gestor (MANUTENCAO_PONTO, RF10).
  Future<AjustePonto> solicitarAjuste({
    required DateTime data,
    required TipoBatida tipoBatida,
    required String horarioSolicitado,
    required String justificativa,
  });
}
