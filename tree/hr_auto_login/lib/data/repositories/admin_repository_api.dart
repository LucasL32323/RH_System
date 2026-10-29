import '../../core/errors/falha.dart';
import '../../core/network/api_client.dart';
import '../../domain/entities/advertencia.dart';
import '../../domain/entities/colaborador_resumo.dart';
import '../../domain/entities/compliance.dart';
import '../../domain/entities/dashboard_admin.dart';
import '../../domain/entities/estrutura_empresa.dart';
import '../../domain/entities/funcionario.dart';
import '../../domain/entities/ponto.dart';
import '../../domain/repositories/admin_repository.dart';
import '../../domain/repositories/ponto_repository.dart';
import 'ponto_repository_api.dart';

/// Telas do Admin ligadas à API .NET (Fase 5). Repositório HÍBRIDO:
///
/// Real (API v1, token da sessão):
///  - funcionários (lista, ficha, cadastro, desligamento/reativação), cargos e departamentos;
///  - advertências (lista, registro com semáforo e régua);
///  - pedidos de ajuste de ponto (lista de pendentes e decisão do RH/Admin);
///  - no dashboard: quadro ativo, semáforo de advertências e ajustes pendentes.
///
/// Ainda em exemplo (a API não tem endpoint): indicadores do dashboard (absenteísmo, horas extras,
/// jornada por setor, banco de horas, clima, alertas), central de compliance e a lista de consultas LGPD.
/// Esses pontos delegam ao [_prototipo] ou devolvem vazio e estão marcados abaixo.
class AdminRepositoryApi implements AdminRepository {
  AdminRepositoryApi(
    this._api, {
    required String? Function() token,
    required PontoRepository ponto,
    required AdminRepository prototipo,
  })  : _token = token,
        _ponto = ponto,
        _prototipo = prototipo;

  final ApiClient _api;
  final String? Function() _token;

  /// Ponto de quem está logado (mesmo repositório do app: /api/v1/ponto e /api/v1/manutencoes-ponto).
  final PontoRepository _ponto;

  /// Fonte dos dados que a API ainda não fornece (ver a documentação da classe).
  final AdminRepository _prototipo;

  /// A tabela CARGO não tem carga horária; a jornada da API é única (40 h, ver Fase 3).
  static const double cargaSemanalPadrao = 40;

  /// Data de nascimento não vem na lista (só na ficha, para RH/Admin).
  static final DateTime _semData = DateTime(1900);

  static const Endereco _enderecoVazio =
      Endereco(cep: '', logradouro: '', numero: '', bairro: '', cidade: '', uf: '');

  // Nome (minúsculo) -> id, para converter o que a tela escolhe (nomes) no que a API pede (ids).
  final Map<String, int> _cargoIds = {};
  final Map<String, int> _departamentoIds = {};

  // Código FUNC -> setor, para preencher as tabelas de advertência e de ajuste (o DTO só traz código e nome).
  final Map<String, String> _setorPorCodigo = {};

  // Pedidos pendentes carregados no dashboard, para montar a correção de batida ao aprovar.
  final Map<String, AjustePonto> _pendentes = {};

  String _tokenObrigatorio() {
    final token = _token();
    if (token == null || token.isEmpty) throw const Falha('Sua sessão expirou. Entre novamente.');
    return token;
  }

  /// Converte erro de leitura do JSON em mensagem para o usuário (a API mudou ou veio incompleta).
  Future<T> _protegido<T>(Future<T> Function() acao) async {
    try {
      return await acao();
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  // ===================================================================================
  // Paginação
  // ===================================================================================

  /// Lê todas as páginas de uma lista { itens, pagina, tamanho, total } (tamanho 100, no máximo 50 páginas).
  Future<List<Map<String, dynamic>>> _todasAsPaginas(String caminho, Map<String, String> filtro) async {
    const tamanho = 100;
    const limiteDePaginas = 50;
    final itens = <Map<String, dynamic>>[];
    for (var pagina = 1; pagina <= limiteDePaginas; pagina++) {
      final json = await _api.getJson(
        caminho,
        token: _tokenObrigatorio(),
        query: {...filtro, 'pagina': '$pagina', 'tamanho': '$tamanho'},
      );
      final lote = (json['itens'] as List? ?? const []).whereType<Map<String, dynamic>>().toList();
      itens.addAll(lote);
      final total = (json['total'] as num?)?.toInt() ?? itens.length;
      if (lote.isEmpty || itens.length >= total) break;
    }
    return itens;
  }

  /// Só o total de uma lista paginada (pede 1 item).
  Future<int> _total(String caminho, Map<String, String> filtro) async {
    final json = await _api.getJson(
      caminho,
      token: _tokenObrigatorio(),
      query: {...filtro, 'pagina': '1', 'tamanho': '1'},
    );
    return (json['total'] as num).toInt();
  }

  // ===================================================================================
  // Dashboard
  // ===================================================================================

  @override
  Future<DashboardAdmin> carregarDashboard() => _protegido(() async {
        // Indicadores sem endpoint na API (absenteísmo, horas extras, clima...): exemplo.
        final base = await _prototipo.carregarDashboard();

        await _garantirSetores(); // as linhas de ajuste e de advertência mostram o setor
        final ajustes = await _listarAjustes(escopo: 'todas', status: 'Pendente');
        final ativos = await _total('/api/v1/funcionarios', const {'ativo': 'true'});
        final situacoes = SituacaoDisciplinar.agrupar(await listarAdvertencias());
        final amarelo = situacoes.where((s) => s.semaforo == Semaforo.amarelo).length;
        final vermelho = situacoes.where((s) => s.semaforo == Semaforo.vermelho).length;

        _pendentes
          ..clear()
          ..addAll({for (final a in ajustes) a.id: a});

        final r = base.resumo;
        return DashboardAdmin(
          referencia: base.referencia,
          resumo: ResumoAdmin(
            colaboradoresAtivos: ativos,
            variacaoAtivosMesAnterior: r.variacaoAtivosMesAnterior,
            absenteismoPercentual: r.absenteismoPercentual,
            absenteismoMesAnterior: r.absenteismoMesAnterior,
            horasExtrasMes: r.horasExtrasMes,
            horasExtrasMesAnterior: r.horasExtrasMesAnterior,
            faltasNoAno: r.faltasNoAno,
            tempoMedioDeCasaAnos: r.tempoMedioDeCasaAnos,
          ),
          ajustes: ajustes,
          jornadaPorSetor: base.jornadaPorSetor,
          semaforo: DistribuicaoSemaforo(
            verde: (ativos - amarelo - vermelho).clamp(0, ativos).toInt(),
            amarelo: amarelo,
            vermelho: vermelho,
          ),
          bancoHoras: base.bancoHoras,
          clima: base.clima,
          alertas: base.alertas,
        );
      });

  Future<List<AjustePonto>> _listarAjustes({required String escopo, String? status}) async {
    final itens = await _todasAsPaginas('/api/v1/manutencoes-ponto', {
      'escopo': escopo,
      if (status != null) 'status': status,
    });
    return [for (final j in itens) PontoRepositoryApi.ajusteDeJson(j, departamentoDe: _setorDe)];
  }

  String _setorDe(String codigo) => _setorPorCodigo[codigo] ?? '—';

  /// Carrega a lista de funcionários uma vez para saber o setor de cada código.
  Future<void> _garantirSetores() async {
    if (_setorPorCodigo.isEmpty) await listarFuncionarios();
  }

  /// Aprova (corrigindo a batida pedida) ou recusa um pedido de ajuste. A API não desfaz decisão.
  @override
  Future<void> decidirAjuste({required String id, required SituacaoAjuste situacao, String? observacao}) =>
      _protegido(() async {
        final numero = int.tryParse(id);
        if (numero == null) throw Falha('Pedido de ajuste inválido: $id.');
        if (situacao == SituacaoAjuste.pendente) {
          throw const Falha('A decisão de um pedido de ajuste não pode ser desfeita.');
        }

        final nota = observacao?.trim() ?? '';
        final corpo = <String, dynamic>{'aprovar': situacao == SituacaoAjuste.aprovado};

        if (situacao == SituacaoAjuste.aprovado) {
          if (nota.isNotEmpty) corpo['observacao'] = nota;
          final pedido = await _pedidoPendente(id);
          // O horário pedido vem no texto da justificativa. Sem ele (pedido feito fora do app), a aprovação é só abono.
          if (RegExp(r'^\d{2}:\d{2}$').hasMatch(pedido.horarioSolicitado)) {
            corpo['batidas'] = {PontoRepositoryApi.campoDaBatida(pedido.tipoBatida): '${pedido.horarioSolicitado}:00'};
          }
        } else {
          // A API exige o motivo da rejeição (mínimo de 5 caracteres).
          corpo['observacao'] = nota.length >= 5 ? nota : 'Pedido recusado pelo RH.';
        }

        await _api.postJson('/api/v1/manutencoes-ponto/$numero/decisao', corpo, token: _tokenObrigatorio());
        _pendentes.remove(id);
      });

  Future<AjustePonto> _pedidoPendente(String id) async {
    var pedido = _pendentes[id];
    if (pedido == null) {
      for (final a in await _listarAjustes(escopo: 'todas', status: 'Pendente')) {
        _pendentes[a.id] = a;
      }
      pedido = _pendentes[id];
    }
    if (pedido == null) throw const Falha('Pedido não encontrado ou já decidido. Atualize a tela.');
    return pedido;
  }

  // ===================================================================================
  // Funcionários
  // ===================================================================================

  @override
  Future<List<Funcionario>> listarFuncionarios() => _protegido(() async {
        final itens = await _todasAsPaginas('/api/v1/funcionarios', const {});
        final lista = [for (final j in itens) funcionarioDeJson(j)]..sort((a, b) => b.id.compareTo(a.id));
        _setorPorCodigo
          ..clear()
          ..addAll({for (final f in lista) f.codigo: f.setor});
        return lista; // mais recentes primeiro
      });

  /// Ficha completa (CPF, RG, nascimento, telefone e endereço). Para RH/Admin consultando outra pessoa,
  /// o próprio GET grava a auditoria `ConsultaDadosPessoais` no servidor (LGPD, RF22).
  @override
  Future<Funcionario> obterFicha(int id, {String consultadoPor = ''}) => _protegido(() async {
        final json = await _api.getJson('/api/v1/funcionarios/${Funcionario.codigoDe(id)}', token: _tokenObrigatorio());
        return funcionarioDeJson(json);
      });

  /// Próximo código = total de cadastros + 1 (o cadastro nunca é apagado e o id é sequencial).
  /// É só informativo: quem define o código de verdade é a API ao cadastrar.
  @override
  Future<String> proximoCodigo() => _protegido(() async {
        final total = await _total('/api/v1/funcionarios', const {});
        return Funcionario.codigoDe(total + 1);
      });

  @override
  Future<Funcionario> cadastrarFuncionario(NovoFuncionario dados) => _protegido(() async {
        final idCargo = await _idCargo(dados.cargo.nome);
        final idDepartamento = await _idDepartamento(dados.setor.nome);

        final json = await _api.postJson(
          '/api/v1/funcionarios',
          {
            'nome': dados.nome.trim(),
            'cpf': dados.cpf,
            'rg': dados.rg.trim(),
            'dataNascimento': _iso(dados.dataNascimento),
            'dataAdmissao': _iso(dados.dataAdmissao),
            'email': dados.email.trim(),
            'telefone': dados.telefone.replaceAll(RegExp(r'\D'), ''),
            'idCargo': idCargo,
            'idDepartamento': idDepartamento,
            'endereco': {
              'cep': dados.endereco.cep,
              'logradouro': dados.endereco.logradouro,
              'numero': dados.endereco.numero,
              if (dados.endereco.complemento.isNotEmpty) 'complemento': dados.endereco.complemento,
              'bairro': dados.endereco.bairro,
              'cidade': dados.endereco.cidade,
              'uf': dados.endereco.uf,
            },
            'perfil': _perfil(dados.nivel),
            'criarAcesso': dados.enviarConvite,
          },
          token: _tokenObrigatorio(),
        );
        if (json == null) throw const ApiException('Resposta da API em formato inesperado.');

        final codigo = json['codigo'] as String;
        return Funcionario(
          id: idDoCodigo(codigo),
          nome: json['nome'] as String? ?? dados.nome,
          cpf: dados.cpf,
          rg: dados.rg,
          dataNascimento: dados.dataNascimento,
          dataAdmissao: dados.dataAdmissao,
          email: dados.email,
          telefone: dados.telefone,
          cargo: dados.cargo,
          setor: dados.setor.nome,
          nivel: dados.nivel,
          endereco: dados.endereco,
        );
      });

  /// "Gestor" não é perfil na API (é quem é gestor de outros): a conta nasce como Colaborador.
  static String _perfil(NivelAcesso nivel) => switch (nivel) {
        NivelAcesso.rh => 'RH',
        NivelAcesso.admin => 'Admin',
        NivelAcesso.colaborador || NivelAcesso.gestor => 'Colaborador',
      };

  @override
  Future<void> alterarSituacaoFuncionario({required int id, required bool ativo}) async {
    final acao = ativo ? 'reativacao' : 'desligamento'; // desligar usa a data de hoje (corpo vazio)
    await _api.postJson(
      '/api/v1/funcionarios/${Funcionario.codigoDe(id)}/$acao',
      const {},
      token: _tokenObrigatorio(),
    );
  }

  /// A API audita sozinha quando a ficha é consultada (ver [obterFicha]); aqui basta fazer a consulta.
  @override
  Future<void> registrarConsultaDadosPessoais({required int funcionarioId, required String consultadoPor}) async {
    await obterFicha(funcionarioId);
  }

  /// A API ainda não tem endpoint para listar o log de consultas (a gravação existe): lista vazia, sem dado inventado.
  @override
  Future<List<ConsultaDadosPessoais>> listarConsultasDadosPessoais() async => const [];

  // ===================================================================================
  // Estrutura da empresa (cargos e departamentos)
  // ===================================================================================

  @override
  Future<EstruturaEmpresa> carregarEstrutura() => _protegido(_atualizarEstrutura);

  Future<EstruturaEmpresa> _atualizarEstrutura() async {
    final token = _tokenObrigatorio();
    final cargos = (await _api.getLista('/api/v1/cargos', token: token)).whereType<Map<String, dynamic>>().toList();
    final setores =
        (await _api.getLista('/api/v1/departamentos', token: token)).whereType<Map<String, dynamic>>().toList();

    _cargoIds
      ..clear()
      ..addAll({for (final c in cargos) _chave(c['nome'] as String): (c['id'] as num).toInt()});
    _departamentoIds
      ..clear()
      ..addAll({for (final s in setores) _chave(s['nome'] as String): (s['id'] as num).toInt()});

    return EstruturaEmpresa(
      cargos: [
        for (final c in cargos)
          Cargo(
            nome: c['nome'] as String,
            cargaHorariaSemanal: cargaSemanalPadrao,
            descricao: c['descricao'] as String?,
          ),
      ],
      // O gestor do departamento não existe no modelo físico (o gestor é de cada funcionário).
      setores: [for (final s in setores) Setor(nome: s['nome'] as String, descricao: s['descricao'] as String?)],
    );
  }

  @override
  Future<void> adicionarCargo(Cargo cargo) => _protegido(() async {
        // CARGO não tem carga horária: só nome e descrição vão para a API.
        final json = await _api.postJson(
          '/api/v1/cargos',
          {'nome': cargo.nome.trim(), if ((cargo.descricao ?? '').trim().isNotEmpty) 'descricao': cargo.descricao!.trim()},
          token: _tokenObrigatorio(),
        );
        final id = json?['id'];
        if (id is num) _cargoIds[_chave(cargo.nome)] = id.toInt();
      });

  @override
  Future<void> adicionarSetor(Setor setor) => _protegido(() async {
        // O gestor do setor não é gravado: o modelo físico liga o gestor ao funcionário, não ao departamento.
        final json = await _api.postJson(
          '/api/v1/departamentos',
          {'nome': setor.nome.trim(), if ((setor.descricao ?? '').trim().isNotEmpty) 'descricao': setor.descricao!.trim()},
          token: _tokenObrigatorio(),
        );
        final id = json?['id'];
        if (id is num) _departamentoIds[_chave(setor.nome)] = id.toInt();
      });

  Future<int> _idCargo(String nome) async {
    var id = _cargoIds[_chave(nome)];
    if (id == null) {
      await _atualizarEstrutura();
      id = _cargoIds[_chave(nome)];
    }
    if (id == null) throw Falha('O cargo "$nome" não existe na API. Cadastre-o em Configurações.');
    return id;
  }

  Future<int> _idDepartamento(String nome) async {
    var id = _departamentoIds[_chave(nome)];
    if (id == null) {
      await _atualizarEstrutura();
      id = _departamentoIds[_chave(nome)];
    }
    if (id == null) throw Falha('O setor "$nome" não existe na API. Cadastre-o em Configurações.');
    return id;
  }

  static String _chave(String nome) => nome.trim().toLowerCase();

  // ===================================================================================
  // Ponto de quem está logado (delegado ao repositório de ponto)
  // ===================================================================================

  @override
  Future<EspelhoPonto> carregarPonto() => _ponto.carregarPonto();

  @override
  Future<Batida> registrarBatida() => _ponto.registrarBatida();

  @override
  Future<AjustePonto> solicitarAjuste({
    required DateTime data,
    required TipoBatida tipoBatida,
    required String horarioSolicitado,
    required String justificativa,
  }) =>
      _ponto.solicitarAjuste(
        data: data,
        tipoBatida: tipoBatida,
        horarioSolicitado: horarioSolicitado,
        justificativa: justificativa,
      );

  // ===================================================================================
  // Advertências
  // ===================================================================================

  @override
  Future<List<Advertencia>> listarAdvertencias() => _protegido(() async {
        await _garantirSetores();
        final itens = await _todasAsPaginas('/api/v1/advertencias', const {});
        return [for (final j in itens) advertenciaDeJson(j, setorDe: _setorDe)];
      });

  /// Registra pela API (quem registra é o usuário do token; [registradaPor] só serve ao protótipo).
  /// A API só conhece Leve, Suspensao e JustaCausa: toda advertência registrada pela tela vai como Leve
  /// (decisão em aberto para motivos de gravidade média/grave). A régua devolve uma SUGESTÃO; ela vira o
  /// retorno daqui (suspensão aguardando o RH) mas não é gravada: o RH registra a suspensão em seguida.
  @override
  Future<Advertencia?> registrarAdvertencia({
    required String codigoFuncionario,
    required MotivoAdvertencia motivo,
    required String descricao,
    required String registradaPor,
  }) =>
      _protegido(() async {
        final json = await _api.postJson(
          '/api/v1/advertencias',
          {
            'codigoFuncionario': codigoFuncionario.trim().toUpperCase(),
            'tipo': 'Leve',
            'motivo': motivo.nome,
            if (descricao.trim().isNotEmpty) 'descricao': descricao.trim(),
          },
          token: _tokenObrigatorio(),
        );
        if (json == null) throw const ApiException('Resposta da API em formato inesperado.');

        final regua = json['regua'] as Map<String, dynamic>?;
        final acao = regua?['acao'] as String? ?? 'Nenhuma';
        if (acao == 'Nenhuma') return null;

        final registrada = advertenciaDeJson(json['advertencia'] as Map<String, dynamic>, setorDe: _setorDe);
        return Advertencia(
          id: '',
          colaborador: registrada.colaborador,
          data: registrada.data,
          motivo: registrada.motivo,
          descricao: regua?['mensagem'] as String? ?? 'Sugestão da régua de consequências.',
          registradaPor: 'Régua automática',
          medida: Medida.suspensao,
          aguardandoAprovacao: true,
        );
      });

  /// A API não tem "aprovar suspensão" (o RH registra a suspensão diretamente via POST /advertencias).
  @override
  Future<void> aprovarSuspensao(String id) async {
    throw const Falha('A aprovação de suspensão pela API ainda não existe. Registre a suspensão como advertência do tipo Suspensão.');
  }

  // ===================================================================================
  // Compliance (sem endpoint na API: exemplo)
  // ===================================================================================

  @override
  Future<CentralCompliance> carregarCompliance() => _prototipo.carregarCompliance();

  @override
  Future<LeituraObrigatoria> publicarAviso({required String titulo, required String tipo, required DateTime expiraEm}) =>
      _prototipo.publicarAviso(titulo: titulo, tipo: tipo, expiraEm: expiraEm);

  // ===================================================================================
  // Conversão JSON -> entidades (pública e estática para os testes)
  // ===================================================================================

  /// FuncionarioResumoDto (lista) ou ficha (GET /funcionarios/{codigo}). Os campos que só a ficha traz
  /// (documentos, endereço, nascimento, telefone, perfil) usam valores vazios na lista.
  static Funcionario funcionarioDeJson(Map<String, dynamic> j) {
    final documentos = j['documentos'] as Map<String, dynamic>?;
    final endereco = j['endereco'] as Map<String, dynamic>?;
    final codigo = j['codigo'] as String;
    final nascimento = j['dataNascimento'];

    return Funcionario(
      id: idDoCodigo(codigo),
      nome: j['nome'] as String,
      cpf: documentos?['cpf'] as String? ?? '',
      rg: documentos?['rg'] as String? ?? '',
      dataNascimento: nascimento is String ? _lerData(nascimento) : _semData,
      dataAdmissao: _lerData(j['dataAdmissao'] as String),
      email: j['email'] as String? ?? '',
      telefone: j['telefone'] as String? ?? '',
      cargo: Cargo(nome: j['cargo'] as String? ?? '', cargaHorariaSemanal: cargaSemanalPadrao),
      setor: j['departamento'] as String? ?? '',
      gestor: j['nomeGestor'] as String?,
      nivel: switch (j['perfil']) {
        'Admin' => NivelAcesso.admin,
        'RH' => NivelAcesso.rh,
        _ => NivelAcesso.colaborador,
      },
      endereco: endereco == null
          ? _enderecoVazio
          : Endereco(
              cep: endereco['cep'] as String? ?? '',
              logradouro: endereco['logradouro'] as String? ?? '',
              numero: endereco['numero'] as String? ?? '',
              complemento: endereco['complemento'] as String? ?? '',
              bairro: endereco['bairro'] as String? ?? '',
              cidade: endereco['cidade'] as String? ?? '',
              uf: endereco['uf'] as String? ?? '',
            ),
      ativo: j['ativo'] as bool? ?? true,
    );
  }

  /// AdvertenciaDto -> [Advertencia]. Suspensão e justa causa viram [Medida.suspensao]; a API não tem
  /// "aguardando aprovação" (a suspensão já nasce registrada), então `aguardandoAprovacao` é sempre false.
  static Advertencia advertenciaDeJson(Map<String, dynamic> j, {String Function(String codigo)? setorDe}) {
    final tipo = j['tipo'] as String;
    final suspensao = tipo == 'Suspensao' || tipo == 'JustaCausa';
    final codigo = j['codigoFuncionario'] as String;
    final nomeMotivo = j['motivo'] as String;

    return Advertencia(
      id: '${j['id']}',
      colaborador: ColaboradorResumo(
        nome: j['nomeFuncionario'] as String? ?? '',
        matricula: codigo,
        departamento: setorDe?.call(codigo) ?? '—',
      ),
      data: _lerData(j['data'] as String),
      motivo: MotivoAdvertencia.padrao.firstWhere(
        (m) => m.nome == nomeMotivo,
        orElse: () => MotivoAdvertencia(nomeMotivo, suspensao ? Gravidade.media : Gravidade.leve),
      ),
      descricao: j['descricao'] as String? ?? '',
      registradaPor: j['nomeResponsavel'] as String? ?? '',
      medida: suspensao ? Medida.suspensao : Medida.advertencia,
      ativa: j['status'] == 'Ativa',
    );
  }

  /// "FUNC-0142" -> 142
  static int idDoCodigo(String codigo) {
    final m = RegExp(r'(\d+)$').firstMatch(codigo.trim());
    if (m == null) throw FormatException('Código inválido: $codigo');
    return int.parse(m.group(1)!);
  }

  static DateTime _lerData(String texto) {
    final m = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(texto);
    if (m == null) throw FormatException('Data inválida: $texto');
    return DateTime(int.parse(m.group(1)!), int.parse(m.group(2)!), int.parse(m.group(3)!));
  }

  static String _iso(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
