import '../../core/errors/falha.dart';
import '../../core/network/api_client.dart';
import '../../domain/entities/colaborador_resumo.dart';
import '../../domain/entities/ponto.dart';
import '../../domain/repositories/ponto_repository.dart';

/// Ponto de quem está logado, na API .NET (Fases 4 e 5):
///  - GET  /api/v1/ponto/hoje             -> { data, horaServidor, batidas[], proximaBatida }
///  - GET  /api/v1/ponto/espelho?de=&ate= -> { dias[], totais, saldoBancoHorasHoras }
///  - POST /api/v1/ponto/batidas          -> 201 { tipo, data, horario, proximaBatida, fechamentoDoDia }
///  - POST /api/v1/manutencoes-ponto      -> 201 ManutencaoPontoDto (pedido de ajuste, RF22)
///  - GET  /api/v1/manutencoes-ponto?escopo=minhas -> página com os meus pedidos
/// O horário vem sempre do servidor (RF07); o app só exibe.
class PontoRepositoryApi implements PontoRepository {
  PontoRepositoryApi(this._api, {required String? Function() token}) : _token = token;

  final ApiClient _api;

  /// Lê o token da sessão na hora da chamada (a sessão pode mudar depois de criado o repositório).
  final String? Function() _token;

  /// Carga diária usada quando a API não devolve o dia (dias futuros da semana).
  static const double _cargaPadraoHoras = 8;

  /// Limites da justificativa na API (RF22): 10 a 500 caracteres.
  static const int justificativaMinima = 10;
  static const int justificativaMaxima = 500;

  static const Map<String, TipoBatida> _tipos = {
    'Entrada': TipoBatida.entrada,
    'SaidaIntervalo': TipoBatida.saidaIntervalo,
    'RetornoIntervalo': TipoBatida.retornoIntervalo,
    'Saida': TipoBatida.saida,
  };

  String _tokenObrigatorio() {
    final token = _token();
    if (token == null || token.isEmpty) throw const Falha('Sua sessão expirou. Entre novamente.');
    return token;
  }

  @override
  Future<EspelhoPonto> carregarPonto() async {
    final token = _tokenObrigatorio();
    final hoje = await _api.getJson('/api/v1/ponto/hoje', token: token);

    // A semana é calculada pela data do servidor, não pela do aparelho.
    final DateTime agora;
    try {
      agora = lerHoraServidor(hoje['horaServidor']);
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
    final dataHoje = DateTime(agora.year, agora.month, agora.day);
    final segunda = DateTime(dataHoje.year, dataHoje.month, dataHoje.day - (dataHoje.weekday - 1));

    final espelho = await _api.getJson(
      '/api/v1/ponto/espelho',
      token: token,
      query: {'de': _iso(segunda), 'ate': _iso(dataHoje)},
    );
    final solicitacoes = await _carregarMinhasSolicitacoes(token);
    return montarEspelho(hoje: hoje, espelho: espelho, minhasSolicitacoes: solicitacoes);
  }

  /// Meus pedidos de ajuste (mais recentes primeiro, como a API devolve). Se a lista falhar, o espelho
  /// continua abrindo: o ponto do dia é mais importante do que o histórico de pedidos.
  Future<List<AjustePonto>> _carregarMinhasSolicitacoes(String token) async {
    try {
      final pagina = await _api.getJson(
        '/api/v1/manutencoes-ponto',
        token: token,
        query: const {'escopo': 'minhas', 'pagina': '1', 'tamanho': '20'},
      );
      return ajustesDaPagina(pagina);
    } on ApiException {
      return const [];
    }
  }

  @override
  Future<Batida> registrarBatida() async {
    final json = await _api.postJson('/api/v1/ponto/batidas', const {}, token: _tokenObrigatorio());
    if (json == null) throw const ApiException('Resposta da API em formato inesperado.');
    try {
      final data = _lerData(json['data'] as String);
      return Batida(tipo: _tipo(json['tipo']), horario: _comHorario(data, json['horario'] as String));
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  /// Pede a manutenção do ponto de [data]. A tabela MANUTENCAO_PONTO guarda só a justificativa; por isso
  /// o tipo de batida e o horário pedidos viajam no começo do texto ("[entrada 08:02] ...") e o gestor
  /// os recebe prontos ao aprovar (ver [codificarJustificativa] e [decodificarJustificativa]).
  @override
  Future<AjustePonto> solicitarAjuste({
    required DateTime data,
    required TipoBatida tipoBatida,
    required String horarioSolicitado,
    required String justificativa,
  }) async {
    final texto = justificativa.trim();
    if (texto.length < justificativaMinima) {
      throw const Falha('Explique o motivo com pelo menos $justificativaMinima caracteres.');
    }
    final horario = horarioSolicitado.trim();
    if (!RegExp(r'^([01]\d|2[0-3]):[0-5]\d$').hasMatch(horario)) {
      throw const Falha('Informe o horário no formato HH:mm (ex.: 08:02).');
    }

    final composta = codificarJustificativa(tipo: tipoBatida, horario: horario, texto: texto);
    if (composta.length > justificativaMaxima) {
      final limite = justificativaMaxima - (composta.length - texto.length);
      throw Falha('A justificativa passou do limite. Use no máximo $limite caracteres.');
    }

    final json = await _api.postJson(
      '/api/v1/manutencoes-ponto',
      {'data': _iso(data), 'justificativa': composta},
      token: _tokenObrigatorio(),
    );
    if (json == null) throw const ApiException('Resposta da API em formato inesperado.');
    return ajusteDeJson(json);
  }

  // ===================================================================================
  // Pedido de ajuste: texto da justificativa <-> tipo/horário
  // ===================================================================================

  static const Map<TipoBatida, String> _chaves = {
    TipoBatida.entrada: 'entrada',
    TipoBatida.saidaIntervalo: 'saidaIntervalo',
    TipoBatida.retornoIntervalo: 'retornoIntervalo',
    TipoBatida.saida: 'saida',
  };

  /// "[entrada 08:02] Esqueci de bater" (o prefixo conta no limite de 500 caracteres da API).
  static String codificarJustificativa({required TipoBatida tipo, required String horario, required String texto}) =>
      '[${_chaves[tipo]} $horario] $texto';

  /// Separa o prefixo da justificativa. Pedidos feitos fora do app (Swagger, por exemplo) não têm prefixo:
  /// nesse caso tipo e horário voltam null e o texto fica inteiro.
  static ({TipoBatida? tipo, String? horario, String texto}) decodificarJustificativa(String bruto) {
    final m = RegExp(r'^\[(entrada|saidaIntervalo|retornoIntervalo|saida) (\d{2}:\d{2})\]\s*([\s\S]*)$').firstMatch(bruto);
    if (m == null) return (tipo: null, horario: null, texto: bruto);
    final tipo = _chaves.entries.firstWhere((e) => e.value == m.group(1)).key;
    return (tipo: tipo, horario: m.group(2), texto: m.group(3)!.trim());
  }

  /// Nome do campo de [BatidasCorrigidasDto] na decisão do pedido (POST .../decisao).
  static String campoDaBatida(TipoBatida tipo) => _chaves[tipo]!;

  // ===================================================================================
  // Conversão JSON -> entidades (pública e estática para os testes)
  // ===================================================================================

  /// ManutencaoPontoDto -> [AjustePonto]. [departamentoDe] preenche o setor quando o chamador o conhece
  /// (o DTO da API só traz código e nome).
  static AjustePonto ajusteDeJson(Map<String, dynamic> json, {String Function(String codigo)? departamentoDe}) {
    try {
      final codigo = json['codigoFuncionario'] as String;
      final dataPonto = _lerData(json['dataPonto'] as String);
      final batidas = [
        for (final b in (json['batidas'] as List? ?? const [])) b as Map<String, dynamic>,
      ];
      final decodificada = decodificarJustificativa(json['justificativa'] as String? ?? '');

      // Pedido sem prefixo: assume a primeira batida que falta no dia (ou a entrada).
      final tipo = decodificada.tipo ?? _primeiraAusente(batidas);

      String? registrado;
      for (final b in batidas) {
        if (_tipo(b['tipo']) == tipo) registrado = _hhmm(b['horario'] as String);
      }

      return AjustePonto(
        id: '${json['id']}',
        colaborador: ColaboradorResumo(
          nome: json['nomeFuncionario'] as String? ?? '',
          matricula: codigo,
          departamento: departamentoDe?.call(codigo) ?? '—',
        ),
        data: dataPonto,
        tipoBatida: tipo,
        horarioRegistrado: registrado,
        horarioSolicitado: decodificada.horario ?? '—',
        justificativa: decodificada.texto,
        enviadoEm: lerHoraServidor(json['dataSolicitacao']),
        situacao: _situacao(json['status']),
        observacao: _textoOuNull(json['observacao']),
      );
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  /// Página { itens, pagina, tamanho, total } de manutenções -> lista de [AjustePonto].
  static List<AjustePonto> ajustesDaPagina(Map<String, dynamic> pagina, {String Function(String codigo)? departamentoDe}) {
    final itens = pagina['itens'];
    if (itens is! List) throw const ApiException('Resposta da API em formato inesperado.');
    return [
      for (final item in itens)
        if (item is Map<String, dynamic>) ajusteDeJson(item, departamentoDe: departamentoDe),
    ];
  }

  /// Texto sem espaços nas pontas; null quando vem null, não é texto ou fica vazio.
  static String? _textoOuNull(Object? valor) {
    if (valor is! String) return null;
    final texto = valor.trim();
    return texto.isEmpty ? null : texto;
  }

  static SituacaoAjuste _situacao(Object? valor) => switch ('$valor') {
        'Pendente' => SituacaoAjuste.pendente,
        'Aprovada' => SituacaoAjuste.aprovado,
        'Rejeitada' => SituacaoAjuste.recusado,
        _ => throw FormatException('Status de manutenção desconhecido: $valor'),
      };

  static TipoBatida _primeiraAusente(List<Map<String, dynamic>> batidas) {
    final feitas = {for (final b in batidas) _tipo(b['tipo'])};
    for (final tipo in TipoBatida.values) {
      if (!feitas.contains(tipo)) return tipo;
    }
    return TipoBatida.entrada;
  }

  /// Junta /ponto/hoje e /ponto/espelho no [EspelhoPonto] da tela: segunda a sexta da semana do servidor
  /// (mais hoje, se for fim de semana). As batidas de hoje vêm de /ponto/hoje, que é o estado mais recente.
  static EspelhoPonto montarEspelho({
    required Map<String, dynamic> hoje,
    required Map<String, dynamic> espelho,
    List<AjustePonto> minhasSolicitacoes = const [],
  }) {
    try {
      final agora = lerHoraServidor(hoje['horaServidor']);
      final dataHoje = DateTime(agora.year, agora.month, agora.day);
      final segunda = DateTime(dataHoje.year, dataHoje.month, dataHoje.day - (dataHoje.weekday - 1));

      final diasDaApi = <DateTime, Map<String, dynamic>>{
        for (final d in (espelho['dias'] as List? ?? const []))
          _lerData((d as Map<String, dynamic>)['data'] as String): d,
      };

      final batidasHoje = [
        for (final b in (hoje['batidas'] as List? ?? const []))
          Batida(
            tipo: _tipo((b as Map<String, dynamic>)['tipo']),
            horario: _comHorario(dataHoje, b['horario'] as String),
          ),
      ];

      final datas = [
        for (var i = 0; i < 5; i++) DateTime(segunda.year, segunda.month, segunda.day + i),
        if (dataHoje.weekday > DateTime.friday) dataHoje,
      ];

      final semana = [
        for (final dia in datas)
          DiaEspelho(
            data: dia,
            cargaHoras: _cargaDoDia(diasDaApi[dia]),
            batidas: dia == dataHoje ? batidasHoje : _batidasDoDia(dia, diasDaApi[dia]),
          ),
      ];

      final saldoHoras = (espelho['saldoBancoHorasHoras'] as num?)?.toDouble() ?? 0;

      return EspelhoPonto(
        agora: agora,
        semana: semana,
        saldoBancoHoras: Duration(minutes: (saldoHoras * 60).round()),
        minhasSolicitacoes: minhasSolicitacoes,
      );
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  /// "2026-10-21T10:30:15.123-03:00" -> 21/10/2026 10:30:15 no relógio do servidor (Brasília).
  /// Usa a hora escrita, sem converter para o fuso do aparelho.
  static DateTime lerHoraServidor(Object? valor) {
    final m = RegExp(r'^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})').firstMatch('${valor ?? ''}');
    if (m == null) throw const FormatException('horaServidor inválida');
    int g(int i) => int.parse(m.group(i)!);
    return DateTime(g(1), g(2), g(3), g(4), g(5), g(6));
  }

  /// Carga do dia deduzida dos totais da API (trabalhado - extras + faltantes), assim o saldo que a tela
  /// calcula bate com o da API (tolerâncias, feriados e fins de semana incluídos).
  static double _cargaDoDia(Map<String, dynamic>? dia) {
    if (dia == null) return _cargaPadraoHoras;
    int minutos(String campo) => (dia[campo] as num?)?.toInt() ?? 0;
    final carga = minutos('trabalhadoMinutos') - minutos('extrasMinutos') + minutos('faltantesMinutos');
    return carga < 0 ? 0 : carga / 60;
  }

  static List<Batida> _batidasDoDia(DateTime dia, Map<String, dynamic>? json) {
    if (json == null) return const [];
    const campos = {
      'entrada': TipoBatida.entrada,
      'saidaIntervalo': TipoBatida.saidaIntervalo,
      'retornoIntervalo': TipoBatida.retornoIntervalo,
      'saida': TipoBatida.saida,
    };
    return [
      for (final c in campos.entries)
        if (json[c.key] is String) Batida(tipo: c.value, horario: _comHorario(dia, json[c.key] as String)),
    ];
  }

  static TipoBatida _tipo(Object? valor) {
    final tipo = _tipos['$valor'];
    if (tipo == null) throw FormatException('Tipo de batida desconhecido: $valor');
    return tipo;
  }

  /// "2026-10-21" -> DateTime(2026, 10, 21)
  static DateTime _lerData(String texto) {
    final m = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(texto);
    if (m == null) throw FormatException('Data inválida: $texto');
    return DateTime(int.parse(m.group(1)!), int.parse(m.group(2)!), int.parse(m.group(3)!));
  }

  /// "08:02:15" -> "08:02"
  static String _hhmm(String horario) {
    final m = RegExp(r'^(\d{2}):(\d{2})').firstMatch(horario);
    if (m == null) throw FormatException('Horário inválido: $horario');
    return '${m.group(1)}:${m.group(2)}';
  }

  /// Dia + "08:02:15" (ou "08:02") -> DateTime.
  static DateTime _comHorario(DateTime dia, String horario) {
    final m = RegExp(r'^(\d{2}):(\d{2})(?::(\d{2}))?').firstMatch(horario);
    if (m == null) throw FormatException('Horário inválido: $horario');
    return DateTime(
      dia.year,
      dia.month,
      dia.day,
      int.parse(m.group(1)!),
      int.parse(m.group(2)!),
      int.parse(m.group(3) ?? '0'),
    );
  }

  static String _iso(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
