import '../entities/advertencia.dart';
import '../entities/compliance.dart';
import '../entities/dashboard_admin.dart';
import '../entities/estrutura_empresa.dart';
import '../entities/funcionario.dart';
import '../entities/ponto.dart';
import 'ponto_repository.dart';

/// Contrato dos dados das telas do Admin. Duas implementações: em memória (protótipo) e a da API
/// (`AdminRepositoryApi`, Fase 5), escolhida em `providers.dart` pelo `--dart-define=PROTOTIPO`.
/// Inclui o [PontoRepository]: no modo protótipo, o mesmo objeto em memória atende a aba de ponto.
abstract class AdminRepository implements PontoRepository {
  // ---- Dashboard ----
  Future<DashboardAdmin> carregarDashboard();

  /// Grava a decisão sobre um ajuste de ponto. [SituacaoAjuste.pendente] desfaz a decisão
  /// (só o protótipo permite; a API recusa). [observacao] é o motivo: obrigatório (mín. 5 caracteres)
  /// ao recusar na API; quando vem vazio, a implementação usa um texto padrão.
  Future<void> decidirAjuste({required String id, required SituacaoAjuste situacao, String? observacao});

  // ---- Funcionários ----
  Future<List<Funcionario>> listarFuncionarios();

  /// Código que o próximo cadastro vai receber (RN10: sequencial e imutável).
  Future<String> proximoCodigo();
  Future<Funcionario> cadastrarFuncionario(NovoFuncionario dados);

  /// Inativa ou reativa (RF04: o cadastro nunca é apagado).
  Future<void> alterarSituacaoFuncionario({required int id, required bool ativo});

  /// Ficha completa (CPF, RG, nascimento, telefone e endereço) de um funcionário. Consultar a ficha
  /// de outra pessoa é auditado (RF22, LGPD): na API quem grava é o servidor (usa o usuário do token);
  /// no protótipo, [consultadoPor] ("FUNC-0001 · Nome") entra no log em memória.
  Future<Funcionario> obterFicha(int id, {String consultadoPor = ''});

  /// Registra quem viu o CPF/RG completos de um funcionário (RF22, LGPD).
  Future<void> registrarConsultaDadosPessoais({required int funcionarioId, required String consultadoPor});
  Future<List<ConsultaDadosPessoais>> listarConsultasDadosPessoais();

  // ---- Estrutura da empresa ----
  Future<EstruturaEmpresa> carregarEstrutura();
  Future<void> adicionarCargo(Cargo cargo);
  Future<void> adicionarSetor(Setor setor);

  // ---- Ponto de quem está logado: carregarPonto, registrarBatida e solicitarAjuste (PontoRepository) ----

  // ---- Advertências ----
  Future<List<Advertencia>> listarAdvertencias();

  /// Registra a advertência e aplica a régua de consequências (RN07).
  /// Devolve a suspensão gerada pela régua, se houver.
  Future<Advertencia?> registrarAdvertencia({
    required String codigoFuncionario,
    required MotivoAdvertencia motivo,
    required String descricao,
    required String registradaPor,
  });
  Future<void> aprovarSuspensao(String id);

  // ---- Compliance ----
  Future<CentralCompliance> carregarCompliance();
  Future<LeituraObrigatoria> publicarAviso({required String titulo, required String tipo, required DateTime expiraEm});
}
