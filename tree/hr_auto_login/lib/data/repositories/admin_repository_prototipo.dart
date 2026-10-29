import '../../core/errors/falha.dart';
import '../../core/formatting/formatadores.dart';
import '../../domain/entities/advertencia.dart';
import '../../domain/entities/colaborador_resumo.dart';
import '../../domain/entities/compliance.dart';
import '../../domain/entities/dashboard_admin.dart';
import '../../domain/entities/estrutura_empresa.dart';
import '../../domain/entities/funcionario.dart';
import '../../domain/entities/ponto.dart';
import '../../domain/repositories/admin_repository.dart';

/// Dados de exemplo das telas do Admin (outubro/2026), em memória e determinísticos:
/// 248 colaboradores ativos + 4 inativos, distribuídos pelos 6 setores. Os indicadores do
/// dashboard são calculados a partir desses dados, então a lista e os números batem.
class AdminRepositoryPrototipo implements AdminRepository {
  AdminRepositoryPrototipo({
    this.latencia = const Duration(milliseconds: 400),
    DateTime Function()? relogio,
  }) : _relogio = relogio ?? DateTime.now {
    _funcionarios = _gerarFuncionarios();
    _advertencias = _advertenciasIniciais();
    _ajustes = _ajustesIniciais();
    _semana = _semanaInicial();
    _compliance = _complianceInicial();
  }

  /// Atraso artificial para ver o carregamento (zero nos testes).
  final Duration latencia;

  /// "Horário do servidor" usado nas batidas de ponto. Fixo nos testes.
  final DateTime Function() _relogio;

  /// Data dos exemplos do dashboard (ajustes, alertas, advertências).
  static final DateTime referencia = DateTime(2026, 10, 21, 10, 30);

  late List<Funcionario> _funcionarios;
  late List<Advertencia> _advertencias;
  late List<AjustePonto> _ajustes;
  late List<DiaEspelho> _semana;
  late CentralCompliance _compliance;
  final List<AjustePonto> _minhasSolicitacoes = [];
  final List<ConsultaDadosPessoais> _consultasLgpd = [];
  int _sequencia = 0;

  static const Duration _saldoAnteriorBancoHoras = Duration(hours: 12, minutes: 30);

  // ===================================================================================
  // Estrutura da empresa
  // ===================================================================================

  static const _operador = Cargo(nome: 'Operador de logística', cargaHorariaSemanal: 44);
  static const _supervisor = Cargo(nome: 'Supervisor de operações', cargaHorariaSemanal: 44);
  static const _consultor = Cargo(nome: 'Consultor comercial', cargaHorariaSemanal: 44);
  static const _coordenador = Cargo(nome: 'Coordenador comercial', cargaHorariaSemanal: 40);
  static const _desenvolvedor = Cargo(nome: 'Desenvolvedor', cargaHorariaSemanal: 40);
  static const _suporte = Cargo(nome: 'Analista de suporte', cargaHorariaSemanal: 40);
  static const _assistente = Cargo(nome: 'Assistente administrativo', cargaHorariaSemanal: 44);
  static const _financeiro = Cargo(nome: 'Analista financeiro', cargaHorariaSemanal: 40);
  static const _analistaRh = Cargo(nome: 'Analista de RH', cargaHorariaSemanal: 40);
  static const _adminSistema = Cargo(nome: 'Administrador do sistema', cargaHorariaSemanal: 40);

  final List<Cargo> _cargos = [
    _operador, _supervisor, _consultor, _coordenador, _desenvolvedor,
    _suporte, _assistente, _financeiro, _analistaRh, _adminSistema,
  ];

  final List<Setor> _setores = [
    const Setor(nome: 'Operações', gestor: 'Sérgio Matos', descricao: 'Logística e armazenagem'),
    const Setor(nome: 'Comercial', gestor: 'Helena Duarte', descricao: 'Vendas e relacionamento'),
    const Setor(nome: 'Tecnologia', gestor: 'Bruno Carvalho', descricao: 'Sistemas e suporte'),
    const Setor(nome: 'Administrativo', gestor: 'Luciana Pires', descricao: 'Compras e facilities'),
    const Setor(nome: 'Financeiro', gestor: 'Marcos Vieira', descricao: 'Contas e tesouraria'),
    const Setor(nome: 'RH', gestor: 'Renata Lopes', descricao: 'Pessoas e cultura'),
  ];

  /// Ativos por setor (soma 248).
  static const Map<String, int> _quadro = {
    'Operações': 92,
    'Comercial': 54,
    'Tecnologia': 38,
    'Administrativo': 27,
    'Financeiro': 19,
    'RH': 18,
  };

  static const Map<String, List<Cargo>> _cargosDoSetor = {
    'Operações': [_operador, _operador, _operador, _supervisor],
    'Comercial': [_consultor, _consultor, _coordenador],
    'Tecnologia': [_desenvolvedor, _suporte],
    'Administrativo': [_assistente],
    'Financeiro': [_financeiro],
    'RH': [_analistaRh],
  };

  /// Pessoas citadas nos exemplos (ajustes, advertências), com código fixo.
  static const List<(int, String, String, Cargo, NivelAcesso)> _fixos = [
    (1, 'Administrador do Sistema', 'RH', _adminSistema, NivelAcesso.admin),
    (2, 'Renata Lopes', 'RH', _analistaRh, NivelAcesso.rh),
    (3, 'Sérgio Matos', 'Operações', _supervisor, NivelAcesso.gestor),
    (4, 'Helena Duarte', 'Comercial', _coordenador, NivelAcesso.gestor),
    (5, 'Bruno Carvalho', 'Tecnologia', _desenvolvedor, NivelAcesso.gestor),
    (6, 'Luciana Pires', 'Administrativo', _assistente, NivelAcesso.gestor),
    (7, 'Marcos Vieira', 'Financeiro', _financeiro, NivelAcesso.gestor),
    (31, 'Carlos Eduardo Lima', 'Financeiro', _financeiro, NivelAcesso.colaborador),
    (58, 'Patrícia Souza', 'Administrativo', _assistente, NivelAcesso.colaborador),
    (64, 'Lucas Ferreira', 'Operações', _operador, NivelAcesso.colaborador),
    (87, 'Rafael Nogueira', 'Operações', _operador, NivelAcesso.colaborador),
    (95, 'Roberto Dias', 'Operações', _operador, NivelAcesso.colaborador),
    (118, 'Beatriz Almeida', 'RH', _analistaRh, NivelAcesso.rh),
    (121, 'André Martins', 'Comercial', _consultor, NivelAcesso.colaborador),
    (142, 'Mariana Costa', 'Comercial', _consultor, NivelAcesso.colaborador),
    (176, 'Thiago Ramos', 'Operações', _operador, NivelAcesso.colaborador),
    (188, 'Fernanda Ribeiro', 'Tecnologia', _suporte, NivelAcesso.colaborador),
    (203, 'Juliana Prado', 'Tecnologia', _desenvolvedor, NivelAcesso.colaborador),
    (230, 'Camila Torres', 'Operações', _operador, NivelAcesso.colaborador),
  ];

  static const Set<int> _inativos = {12, 57, 99, 160};
  static const int _totalCadastros = 252;

  static const _nomes = [
    'Ana', 'Bruno', 'Carla', 'Daniel', 'Eduarda', 'Felipe', 'Gabriela', 'Henrique', 'Isabela', 'João',
    'Larissa', 'Mateus', 'Natália', 'Otávio', 'Paula', 'Ricardo', 'Sofia', 'Tiago', 'Vanessa', 'Vinícius',
    'Aline', 'Caio', 'Débora', 'Elias', 'Fábio', 'Giovana', 'Igor', 'Jéssica', 'Leandro', 'Mônica',
    'Nelson', 'Priscila', 'Rodrigo', 'Simone', 'Tatiane', 'Úrsula', 'Wagner', 'Yasmin', 'Alexandre', 'Bianca',
  ];

  static const _sobrenomes = [
    'Silva', 'Santos', 'Oliveira', 'Souza', 'Rodrigues', 'Ferreira', 'Alves', 'Pereira', 'Gomes', 'Ribeiro',
    'Carvalho', 'Almeida', 'Lopes', 'Martins', 'Araújo', 'Barbosa', 'Rocha', 'Dias', 'Moreira', 'Nunes',
    'Mendes', 'Cardoso', 'Teixeira', 'Freitas', 'Castro', 'Pinto', 'Moura', 'Campos', 'Correia', 'Vieira',
    'Monteiro', 'Batista', 'Fonseca', 'Machado', 'Guimarães', 'Brito', 'Ramos', 'Farias', 'Tavares', 'Xavier',
  ];

  static const _ruas = [
    'Rua Azevedo Sodré', 'Avenida Ana Costa', 'Rua Oswaldo Cruz', 'Avenida Conselheiro Nébias',
    'Rua Goiás', 'Avenida Bartolomeu de Gusmão', 'Rua Tolentino Filgueiras', 'Rua Mato Grosso',
  ];
  static const _bairros = ['Gonzaga', 'Boqueirão', 'Embaré', 'Aparecida', 'Ponta da Praia', 'Vila Mathias'];

  List<Funcionario> _gerarFuncionarios() {
    final restantes = Map<String, int>.of(_quadro);
    final fixos = {for (final f in _fixos) f.$1: f};
    for (final f in _fixos) {
      restantes[f.$3] = restantes[f.$3]! - 1;
    }

    final setoresEmOrdem = _quadro.keys.toList();
    var proximoSetor = 0;
    String sortearSetor() {
      while (restantes[setoresEmOrdem[proximoSetor % setoresEmOrdem.length]]! <= 0) {
        proximoSetor++;
      }
      final setor = setoresEmOrdem[proximoSetor % setoresEmOrdem.length];
      restantes[setor] = restantes[setor]! - 1;
      proximoSetor++;
      return setor;
    }

    final gestorDo = {for (final s in _setores) s.nome: s.gestor};
    final lista = <Funcionario>[];
    for (var id = 1; id <= _totalCadastros; id++) {
      final fixo = fixos[id];
      final inativo = _inativos.contains(id);
      final setor = fixo?.$3 ?? (inativo ? 'Operações' : sortearSetor());
      final cargos = _cargosDoSetor[setor]!;
      final nome = fixo?.$2 ?? '${_nomes[id % _nomes.length]} ${_sobrenomes[(id * 7 + id ~/ _nomes.length) % _sobrenomes.length]}';
      final nivel = fixo?.$5 ?? NivelAcesso.colaborador;
      lista.add(Funcionario(
        id: id,
        nome: nome,
        cpf: _cpfDe(id),
        rg: _rgDe(id),
        dataNascimento: DateTime(1972 + id % 30, 1 + id % 12, 1 + id % 28),
        dataAdmissao: id == 1 ? DateTime(2019, 3, 11) : DateTime(2012 + id % 14, 1 + (id * 5) % 12, 1 + (id * 3) % 28),
        email: '${_slug(nome)}@hrauto.local',
        telefone: _telefoneDe(id),
        cargo: fixo?.$4 ?? cargos[id % cargos.length],
        setor: setor,
        gestor: nivel == NivelAcesso.gestor ? null : gestorDo[setor],
        nivel: nivel,
        ativo: !inativo,
        endereco: Endereco(
          cep: '110${(10000 + id * 137) % 90000 + 10000}'.substring(0, 8),
          logradouro: _ruas[id % _ruas.length],
          numero: '${(id * 13) % 900 + 10}',
          bairro: _bairros[id % _bairros.length],
          cidade: 'Santos',
          uf: 'SP',
        ),
      ));
    }
    _sequencia = _totalCadastros;
    return lista;
  }

  static String _slug(String nome) {
    final partes = Formatadores.normalizarBusca(nome).split(' ').where((p) => p.isNotEmpty).toList();
    return partes.length == 1 ? partes.first : '${partes.first}.${partes.last}';
  }

  /// CPF fictício com dígitos verificadores válidos.
  static String _cpfDe(int id) {
    final base = ((id * 7919 + 104729) % 900000000 + 100000000).toString();
    final digitos = base.split('').map(int.parse).toList();
    for (var etapa = 0; etapa < 2; etapa++) {
      var soma = 0;
      for (var i = 0; i < digitos.length; i++) {
        soma += digitos[i] * (digitos.length + 1 - i);
      }
      final resto = soma % 11;
      digitos.add(resto < 2 ? 0 : 11 - resto);
    }
    return digitos.join();
  }

  static String _telefoneDe(int id) {
    final d = ((81000000 + id * 3917) % 100000000).toString().padLeft(8, '0');
    return '(13) 9${d.substring(0, 4)}-${d.substring(4)}';
  }

  static String _rgDe(int id) {
    final n = (id * 48271 + 1234567) % 90000000 + 10000000;
    final s = n.toString();
    return '${s.substring(0, 2)}.${s.substring(2, 5)}.${s.substring(5, 8)}-${id % 10}';
  }

  Funcionario? _porCodigo(String codigo) {
    final normalizado = codigo.trim().toUpperCase();
    for (final f in _funcionarios) {
      if (f.codigo == normalizado) return f;
    }
    return null;
  }

  ColaboradorResumo _resumo(int id) => _funcionarios[id - 1].resumo;

  Future<void> _esperar() => Future<void>.delayed(latencia);

  // ===================================================================================
  // Dashboard
  // ===================================================================================

  List<AjustePonto> _ajustesIniciais() => [
        AjustePonto(
          id: 'aj-0412',
          colaborador: _resumo(87),
          data: DateTime(2026, 10, 20),
          tipoBatida: TipoBatida.entrada,
          horarioRegistrado: '08:47',
          horarioSolicitado: '08:02',
          justificativa: 'O app não abriu na portaria; cheguei no horário.',
          enviadoEm: DateTime(2026, 10, 21, 8, 10),
        ),
        AjustePonto(
          id: 'aj-0411',
          colaborador: _resumo(203),
          data: DateTime(2026, 10, 17),
          tipoBatida: TipoBatida.saida,
          horarioSolicitado: '19:12',
          justificativa: 'Esqueci de registrar a saída depois da reunião com o cliente.',
          enviadoEm: DateTime(2026, 10, 20, 18, 40),
        ),
        AjustePonto(
          id: 'aj-0410',
          colaborador: _resumo(176),
          data: DateTime(2026, 10, 19),
          tipoBatida: TipoBatida.retornoIntervalo,
          horarioRegistrado: '13:58',
          horarioSolicitado: '13:00',
          justificativa: 'Registrei o retorno atrasado; voltei às 13:00.',
          enviadoEm: DateTime(2026, 10, 20, 9, 5),
        ),
        AjustePonto(
          id: 'aj-0409',
          colaborador: _resumo(142),
          data: DateTime(2026, 10, 16),
          tipoBatida: TipoBatida.saida,
          horarioRegistrado: '18:02',
          horarioSolicitado: '20:15',
          justificativa: 'Visita a cliente no Guarujá depois do expediente.',
          enviadoEm: DateTime(2026, 10, 19, 11, 27),
        ),
        AjustePonto(
          id: 'aj-0408',
          colaborador: _resumo(58),
          data: DateTime(2026, 10, 15),
          tipoBatida: TipoBatida.entrada,
          horarioSolicitado: '08:00',
          justificativa: 'Batida offline não sincronizou.',
          enviadoEm: DateTime(2026, 10, 16, 8, 30),
        ),
      ];

  @override
  Future<DashboardAdmin> carregarDashboard() async {
    await _esperar();

    final ativos = _funcionarios.where((f) => f.ativo).toList();
    final situacoes = SituacaoDisciplinar.agrupar(_advertencias);
    final amarelo = situacoes.where((s) => s.semaforo == Semaforo.amarelo).length;
    final vermelho = situacoes.where((s) => s.semaforo == Semaforo.vermelho).length;
    final diasDeCasa = ativos.fold<int>(0, (soma, f) => soma + referencia.difference(f.dataAdmissao).inDays);
    final suspensoesPendentes = _advertencias.where((a) => a.aguardandoAprovacao).toList();

    return DashboardAdmin(
      referencia: referencia,
      resumo: ResumoAdmin(
        colaboradoresAtivos: ativos.length,
        variacaoAtivosMesAnterior: 6,
        absenteismoPercentual: 2.8,
        absenteismoMesAnterior: 3.2,
        horasExtrasMes: 312,
        horasExtrasMesAnterior: 289,
        faltasNoAno: 186,
        tempoMedioDeCasaAnos: ativos.isEmpty ? 0 : diasDeCasa / ativos.length / 365.25,
      ),
      ajustes: List.of(_ajustes),
      jornadaPorSetor: const [
        JornadaSetor(setor: 'Operações', horasAtraso: 38.5, horasExtras: 146),
        JornadaSetor(setor: 'Comercial', horasAtraso: 21, horasExtras: 64),
        JornadaSetor(setor: 'Tecnologia', horasAtraso: 9.5, horasExtras: 52),
        JornadaSetor(setor: 'Financeiro', horasAtraso: 4, horasExtras: 22),
        JornadaSetor(setor: 'Administrativo', horasAtraso: 7, horasExtras: 18),
        JornadaSetor(setor: 'RH', horasAtraso: 3.5, horasExtras: 10),
      ],
      semaforo: DistribuicaoSemaforo(
        verde: ativos.length - amarelo - vermelho,
        amarelo: amarelo,
        vermelho: vermelho,
      ),
      bancoHoras: const BancoHorasEmpresa(creditoHoras: 1284, debitoHoras: 376, colaboradoresComSaldoNegativo: 23),
      clima: const ClimaDoDia(
        respostas: {
          HumorClima.otimo: 58,
          HumorClima.bem: 74,
          HumorClima.neutro: 31,
          HumorClima.cansado: 22,
          HumorClima.estressado: 9,
        },
        publico: 248,
      ),
      alertas: [
        for (final s in suspensoesPendentes)
          AlertaRh(
            tipo: TipoAlerta.regua,
            titulo: 'Suspensão aguardando aprovação',
            descricao: '${s.colaborador.nome} (${s.colaborador.matricula}) atingiu '
                '${ReguaConsequencias.levesParaSuspensao} advertências leves.',
            momento: s.data,
          ),
        AlertaRh(
          tipo: TipoAlerta.horasExtras,
          titulo: '3 colaboradores passaram de 2 h extras ontem',
          descricao: 'Rafael Nogueira (2h40), Thiago Ramos (2h15) e Lucas Ferreira (2h05), de Operações.',
          momento: DateTime(2026, 10, 21, 7, 0),
        ),
        AlertaRh(
          tipo: TipoAlerta.clima,
          titulo: 'Alerta confidencial de clima',
          descricao: '1 colaborador respondeu "Estressado" 3 dias seguidos. Só o RH vê quem é.',
          momento: DateTime(2026, 10, 21, 9, 0),
        ),
        AlertaRh(
          tipo: TipoAlerta.pontoOffline,
          titulo: '4 batidas offline para conferência',
          descricao: 'Sincronizadas hoje às 08:15; aguardam a conferência dos gestores.',
          momento: DateTime(2026, 10, 21, 8, 15),
        ),
      ],
    );
  }

  @override
  Future<void> decidirAjuste({required String id, required SituacaoAjuste situacao, String? observacao}) async {
    await _esperar();
    final i = _ajustes.indexWhere((a) => a.id == id);
    if (i < 0) throw Falha('Ajuste $id não encontrado.');
    // A observação (motivo da recusa) fica no pedido; voltar para pendente a apaga.
    _ajustes[i] = _ajustes[i].copyWith(situacao: situacao, observacao: observacao);
  }

  // ===================================================================================
  // Funcionários
  // ===================================================================================

  @override
  Future<List<Funcionario>> listarFuncionarios() async {
    await _esperar();
    return List.of(_funcionarios.reversed); // mais recentes primeiro
  }

  @override
  Future<String> proximoCodigo() async => Funcionario.codigoDe(_sequencia + 1);

  @override
  Future<Funcionario> cadastrarFuncionario(NovoFuncionario dados) async {
    await _esperar();
    final cpfEmUso = _funcionarios.any((f) => f.cpf == dados.cpf);
    if (cpfEmUso) throw const Falha('Já existe um funcionário com este CPF.');
    final emailEmUso = _funcionarios.any((f) => f.email.toLowerCase() == dados.email.toLowerCase());
    if (emailEmUso) throw const Falha('Este e-mail já está em uso.');

    _sequencia++;
    final novo = Funcionario(
      id: _sequencia,
      nome: dados.nome,
      cpf: dados.cpf,
      rg: dados.rg,
      dataNascimento: dados.dataNascimento,
      dataAdmissao: dados.dataAdmissao,
      email: dados.email,
      telefone: dados.telefone,
      cargo: dados.cargo,
      setor: dados.setor.nome,
      gestor: dados.nivel == NivelAcesso.gestor ? null : dados.setor.gestor,
      nivel: dados.nivel,
      endereco: dados.endereco,
    );
    _funcionarios.add(novo);
    return novo;
  }

  @override
  Future<void> alterarSituacaoFuncionario({required int id, required bool ativo}) async {
    await _esperar();
    final i = _funcionarios.indexWhere((f) => f.id == id);
    if (i < 0) throw Falha('Funcionário $id não encontrado.');
    _funcionarios[i] = _funcionarios[i].copyWith(ativo: ativo);
  }

  @override
  Future<Funcionario> obterFicha(int id, {String consultadoPor = ''}) async {
    await _esperar();
    final i = _funcionarios.indexWhere((f) => f.id == id);
    if (i < 0) throw Falha('Funcionário $id não encontrado.');
    final f = _funcionarios[i];
    // No protótipo a consulta é registrada aqui (na API, o servidor audita).
    if (consultadoPor.isNotEmpty) {
      _consultasLgpd.insert(0, ConsultaDadosPessoais(funcionario: f.resumo, consultadoPor: consultadoPor, momento: _relogio()));
    }
    return f;
  }

  @override
  Future<void> registrarConsultaDadosPessoais({required int funcionarioId, required String consultadoPor}) async {
    final f = _funcionarios.firstWhere((f) => f.id == funcionarioId);
    _consultasLgpd.insert(0, ConsultaDadosPessoais(funcionario: f.resumo, consultadoPor: consultadoPor, momento: _relogio()));
  }

  @override
  Future<List<ConsultaDadosPessoais>> listarConsultasDadosPessoais() async {
    await _esperar();
    return List.of(_consultasLgpd);
  }

  // ===================================================================================
  // Estrutura
  // ===================================================================================

  @override
  Future<EstruturaEmpresa> carregarEstrutura() async {
    await _esperar();
    return EstruturaEmpresa(cargos: List.of(_cargos), setores: List.of(_setores));
  }

  @override
  Future<void> adicionarCargo(Cargo cargo) async {
    await _esperar();
    if (_cargos.contains(cargo)) throw const Falha('Já existe um cargo com este nome.');
    _cargos.add(cargo);
  }

  @override
  Future<void> adicionarSetor(Setor setor) async {
    await _esperar();
    if (_setores.contains(setor)) throw const Falha('Já existe um setor com este nome.');
    _setores.add(setor);
  }

  // ===================================================================================
  // Ponto do usuário logado (Administrador do Sistema, 40 h semanais = 8 h por dia)
  // ===================================================================================

  static const double _cargaDiaria = 8;

  List<DiaEspelho> _semanaInicial() {
    final agora = _relogio();
    final hoje = DateTime(agora.year, agora.month, agora.day);
    final segunda = hoje.subtract(Duration(days: hoje.weekday - 1));
    final dias = <DiaEspelho>[];
    for (var i = 0; i < 5; i++) {
      final dia = segunda.add(Duration(days: i));
      final passado = dia.isBefore(hoje);
      DateTime h(int hora, int minuto) => DateTime(dia.year, dia.month, dia.day, hora, minuto);
      dias.add(DiaEspelho(
        data: dia,
        cargaHoras: _cargaDiaria,
        batidas: passado
            ? [
                Batida(tipo: TipoBatida.entrada, horario: h(8, 2 + i)),
                Batida(tipo: TipoBatida.saidaIntervalo, horario: h(12, 1 + i)),
                Batida(tipo: TipoBatida.retornoIntervalo, horario: h(13, 0 + i)),
                Batida(tipo: TipoBatida.saida, horario: h(17, 10 + i * 9)),
              ]
            : const [],
      ));
    }
    if (hoje.weekday > DateTime.friday) {
      dias.add(DiaEspelho(data: hoje, cargaHoras: 0, batidas: const []));
    }
    return dias;
  }

  EspelhoPonto _espelho() {
    final saldoSemana = _semana.fold<Duration>(Duration.zero, (soma, d) => soma + d.saldo);
    return EspelhoPonto(
      agora: _relogio(),
      semana: List.of(_semana),
      saldoBancoHoras: _saldoAnteriorBancoHoras + saldoSemana,
      minhasSolicitacoes: List.of(_minhasSolicitacoes),
    );
  }

  @override
  Future<EspelhoPonto> carregarPonto() async {
    await _esperar();
    return _espelho();
  }

  @override
  Future<Batida> registrarBatida() async {
    await _esperar();
    final agora = _relogio();
    final i = _semana.indexWhere((d) => d.data.year == agora.year && d.data.month == agora.month && d.data.day == agora.day);
    if (i < 0) throw const Falha('Hoje não está no espelho da semana.');

    final tipo = _espelho().proximaBatida;
    if (tipo == null) throw const Falha('As quatro batidas de hoje já foram registradas.');
    final batida = Batida(tipo: tipo, horario: agora);
    _semana[i] = DiaEspelho(data: _semana[i].data, cargaHoras: _semana[i].cargaHoras, batidas: [..._semana[i].batidas, batida]);
    return batida;
  }

  @override
  Future<AjustePonto> solicitarAjuste({
    required DateTime data,
    required TipoBatida tipoBatida,
    required String horarioSolicitado,
    required String justificativa,
  }) async {
    await _esperar();
    final dia = _semana.where((d) => d.data == DateTime(data.year, data.month, data.day)).firstOrNull;
    final registrada = dia?.batida(tipoBatida);
    final ajuste = AjustePonto(
      id: 'aj-${500 + _minhasSolicitacoes.length}',
      colaborador: _resumo(1),
      data: data,
      tipoBatida: tipoBatida,
      horarioRegistrado: registrada == null ? null : Formatadores.hora(registrada.horario),
      horarioSolicitado: horarioSolicitado,
      justificativa: justificativa,
      enviadoEm: _relogio(),
    );
    _minhasSolicitacoes.insert(0, ajuste);
    return ajuste;
  }

  // ===================================================================================
  // Advertências
  // ===================================================================================

  List<Advertencia> _advertenciasIniciais() {
    var n = 300;
    Advertencia a(int id, DateTime data, int motivo, String descricao,
            {Medida medida = Medida.advertencia, String por = 'Sérgio Matos'}) =>
        Advertencia(
          id: 'adv-${n++}',
          colaborador: _resumo(id),
          data: data,
          motivo: MotivoAdvertencia.padrao[motivo],
          descricao: descricao,
          registradaPor: por,
          medida: medida,
        );

    return [
      a(95, DateTime(2026, 8, 12), 0, 'Chegou 40 min atrasado sem aviso.'),
      a(95, DateTime(2026, 9, 3), 0, 'Terceiro atraso no mês.'),
      a(95, DateTime(2026, 9, 24), 1, 'Uso do celular na operação da empilhadeira.'),
      a(95, DateTime(2026, 9, 25), 0, 'Gerada pela régua de consequências.', medida: Medida.suspensao, por: 'Renata Lopes'),
      a(176, DateTime(2026, 10, 3), 4, 'Sem capacete na área de carga.'),
      a(176, DateTime(2026, 10, 9), 0, 'Atraso de 25 min.'),
      a(176, DateTime(2026, 10, 15), 1, 'Celular durante a conferência de carga.'),
      a(64, DateTime(2026, 9, 2), 0, 'Atraso de 30 min.'),
      a(64, DateTime(2026, 10, 8), 0, 'Atraso de 20 min.'),
      a(121, DateTime(2026, 10, 14), 2, 'Faltou sem justificativa.', por: 'Helena Duarte'),
      a(188, DateTime(2026, 10, 6), 3, 'Instalou software sem autorização.', por: 'Bruno Carvalho'),
      a(230, DateTime(2026, 10, 1), 0, 'Atraso de 15 min.'),
    ];
  }

  @override
  Future<List<Advertencia>> listarAdvertencias() async {
    await _esperar();
    return List.of(_advertencias);
  }

  @override
  Future<Advertencia?> registrarAdvertencia({
    required String codigoFuncionario,
    required MotivoAdvertencia motivo,
    required String descricao,
    required String registradaPor,
  }) async {
    await _esperar();
    final funcionario = _porCodigo(codigoFuncionario);
    if (funcionario == null) throw Falha('Nenhum funcionário com o código ${codigoFuncionario.toUpperCase()}.');
    if (!funcionario.ativo) throw Falha('${funcionario.codigo} está inativo.');

    _advertencias.add(Advertencia(
      id: 'adv-${300 + _advertencias.length}',
      colaborador: funcionario.resumo,
      data: _relogio(),
      motivo: motivo,
      descricao: descricao,
      registradaPor: registradaPor,
    ));

    // RN07: ao completar 3 leves, a régua gera a suspensão, que aguarda o RH.
    final situacao = SituacaoDisciplinar.agrupar(_advertencias)
        .firstWhere((s) => s.colaborador.matricula == funcionario.codigo);
    if (motivo.gravidade == Gravidade.leve && situacao.levesSemSuspensao >= ReguaConsequencias.levesParaSuspensao) {
      final suspensao = Advertencia(
        id: 'adv-${300 + _advertencias.length}',
        colaborador: funcionario.resumo,
        data: _relogio(),
        motivo: motivo,
        descricao: 'Gerada pela régua de consequências.',
        registradaPor: 'Régua automática',
        medida: Medida.suspensao,
        aguardandoAprovacao: true,
      );
      _advertencias.add(suspensao);
      return suspensao;
    }
    return null;
  }

  @override
  Future<void> aprovarSuspensao(String id) async {
    await _esperar();
    final i = _advertencias.indexWhere((a) => a.id == id);
    if (i < 0) throw Falha('Suspensão $id não encontrada.');
    _advertencias[i] = _advertencias[i].copyWith(aguardandoAprovacao: false);
  }

  // ===================================================================================
  // Compliance
  // ===================================================================================

  CentralCompliance _complianceInicial() => CentralCompliance(
        leituras: [
          LeituraObrigatoria(
            id: 'av-41',
            titulo: 'Comunicado: SIPAT 2026',
            tipo: 'Comunicado',
            publicadoEm: DateTime(2026, 10, 18),
            expiraEm: DateTime(2026, 10, 30),
            visualizacoes: 139,
            publico: 248,
          ),
          LeituraObrigatoria(
            id: 'av-40',
            titulo: 'Norma de uso de EPI - Operações',
            tipo: 'Norma',
            publicadoEm: DateTime(2026, 10, 10),
            expiraEm: DateTime(2026, 11, 10),
            visualizacoes: 74,
            publico: 92,
          ),
          LeituraObrigatoria(
            id: 'av-38',
            titulo: 'Código de Conduta 2026',
            tipo: 'Política',
            publicadoEm: DateTime(2026, 10, 1),
            expiraEm: DateTime(2026, 10, 31),
            visualizacoes: 212,
            publico: 248,
          ),
          LeituraObrigatoria(
            id: 'av-35',
            titulo: 'Política de Privacidade e LGPD',
            tipo: 'Política',
            publicadoEm: DateTime(2026, 9, 15),
            expiraEm: DateTime(2026, 11, 15),
            visualizacoes: 231,
            publico: 248,
          ),
        ],
        testes: [
          TesteObrigatorio(
            titulo: 'Simulado de incêndio',
            tipo: 'Simulado',
            perguntas: 10,
            respondidos: 198,
            publico: 248,
            prazo: DateTime(2026, 10, 31),
          ),
          TesteObrigatorio(
            titulo: 'Aceite do código de conduta',
            tipo: 'Aceite',
            perguntas: 3,
            respondidos: 205,
            publico: 248,
            prazo: DateTime(2026, 10, 31),
          ),
          TesteObrigatorio(
            titulo: 'Avaliação NR-35 (trabalho em altura)',
            tipo: 'Teste',
            perguntas: 12,
            respondidos: 61,
            publico: 92,
            prazo: DateTime(2026, 11, 14),
          ),
        ],
        perfilDisc: const PerfilDisc(dominancia: 34, influencia: 51, estabilidade: 72, conformidade: 39, publico: 248),
        beneficios: const [
          Beneficio(nome: 'Vale-refeição', descricao: r'R$ 38,00 por dia útil', aderentes: 236, publico: 248),
          Beneficio(nome: 'Plano de saúde', descricao: 'Enfermaria, com coparticipação', aderentes: 221, publico: 248),
          Beneficio(nome: 'Seguro de vida', descricao: 'Para todos os colaboradores', aderentes: 248, publico: 248),
          Beneficio(nome: 'Plano odontológico', descricao: 'Opcional, desconto em folha', aderentes: 164, publico: 248),
          Beneficio(nome: 'Academia (Gympass)', descricao: 'Plano básico subsidiado', aderentes: 97, publico: 248),
          Beneficio(nome: 'Auxílio home office', descricao: r'R$ 120,00 por mês', aderentes: 36, publico: 38),
        ],
      );

  @override
  Future<CentralCompliance> carregarCompliance() async {
    await _esperar();
    return _compliance;
  }

  @override
  Future<LeituraObrigatoria> publicarAviso({required String titulo, required String tipo, required DateTime expiraEm}) async {
    await _esperar();
    final agora = _relogio();
    final aviso = LeituraObrigatoria(
      id: 'av-${42 + _compliance.leituras.length}',
      titulo: titulo,
      tipo: tipo,
      publicadoEm: agora,
      expiraEm: expiraEm,
      visualizacoes: 0,
      publico: _funcionarios.where((f) => f.ativo).length,
    );
    _compliance = _compliance.copyWith(leituras: [aviso, ..._compliance.leituras]);
    return aviso;
  }
}
