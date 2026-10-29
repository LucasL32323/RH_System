import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/estrutura_empresa.dart';
import '../../domain/entities/funcionario.dart';
import '../providers.dart';
import 'dashboard_admin_view_model.dart';

/// Lista de funcionários (tela "Funcionários") e o cadastro da tela "Novo funcionário".
class FuncionariosViewModel extends AsyncNotifier<List<Funcionario>> {
  @override
  Future<List<Funcionario>> build() => ref.read(adminRepositoryProvider).listarFuncionarios();

  /// Cadastra e coloca o novo funcionário no topo da lista. Lança [Falha] se o CPF ou o e-mail já existirem.
  Future<Funcionario> cadastrar(NovoFuncionario dados) async {
    final novo = await ref.read(adminRepositoryProvider).cadastrarFuncionario(dados);
    state = AsyncData([novo, ...state.valueOrNull ?? const []]);
    ref.invalidate(proximoCodigoProvider);
    ref.invalidate(dashboardAdminViewModelProvider); // o quadro ativo mudou
    return novo;
  }

  /// Inativa ou reativa. Lança [Falha] se não gravar.
  Future<void> alterarSituacao(Funcionario funcionario, {required bool ativo}) async {
    await ref.read(adminRepositoryProvider).alterarSituacaoFuncionario(id: funcionario.id, ativo: ativo);
    state = AsyncData([
      for (final f in state.valueOrNull ?? const <Funcionario>[]) f.id == funcionario.id ? f.copyWith(ativo: ativo) : f,
    ]);
    ref.invalidate(dashboardAdminViewModelProvider);
  }

  /// Busca a ficha completa (CPF, RG, nascimento, telefone, endereço) e a devolve para a tela.
  /// Na API, essa consulta é auditada pelo servidor (RF22, LGPD); no protótipo, [consultadoPor]
  /// entra no log em memória. A linha da lista passa a ter os dados completos, mas mantém a
  /// situação Ativo/Inativo que a lista já mostrava. Lança [Falha] se a consulta não for concluída.
  Future<Funcionario> consultarFicha(Funcionario funcionario, {required String consultadoPor}) async {
    final ficha = await ref.read(adminRepositoryProvider).obterFicha(funcionario.id, consultadoPor: consultadoPor);
    state = AsyncData([
      for (final f in state.valueOrNull ?? const <Funcionario>[])
        f.id == ficha.id ? ficha.copyWith(ativo: f.ativo) : f,
    ]);
    ref.invalidate(consultasDadosPessoaisProvider);
    return ficha;
  }
}

final funcionariosViewModelProvider =
    AsyncNotifierProvider<FuncionariosViewModel, List<Funcionario>>(FuncionariosViewModel.new);

/// Código FUNC que o próximo cadastro vai receber.
final proximoCodigoProvider = FutureProvider<String>((ref) => ref.read(adminRepositoryProvider).proximoCodigo());

/// Histórico de consultas a dados pessoais (Configurações > Privacidade).
final consultasDadosPessoaisProvider = FutureProvider<List<ConsultaDadosPessoais>>(
  (ref) => ref.read(adminRepositoryProvider).listarConsultasDadosPessoais(),
);

/// Cargos e setores (cadastro, filtros e Configurações > Estrutura da empresa).
class EstruturaViewModel extends AsyncNotifier<EstruturaEmpresa> {
  @override
  Future<EstruturaEmpresa> build() => ref.read(adminRepositoryProvider).carregarEstrutura();

  Future<void> adicionarCargo(Cargo cargo) async {
    await ref.read(adminRepositoryProvider).adicionarCargo(cargo);
    state = AsyncData(await ref.read(adminRepositoryProvider).carregarEstrutura());
  }

  Future<void> adicionarSetor(Setor setor) async {
    await ref.read(adminRepositoryProvider).adicionarSetor(setor);
    state = AsyncData(await ref.read(adminRepositoryProvider).carregarEstrutura());
  }
}

final estruturaViewModelProvider =
    AsyncNotifierProvider<EstruturaViewModel, EstruturaEmpresa>(EstruturaViewModel.new);
