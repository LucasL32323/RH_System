import 'dart:typed_data';

import '../../core/errors/falha.dart';
import '../../core/network/api_client.dart';
import '../../domain/entities/holerite.dart';
import '../../domain/regras/regras_holerite.dart';
import '../../domain/repositories/holerite_repository.dart';

/// Holerites na API .NET (RF29, RF30; ver `dia_2026-10-24_fase4.md`):
///  - GET  /api/v1/holerites?codigo=&ano=   -> [ HoleriteDto ]  (sem código = os de quem está logado)
///  - GET  /api/v1/holerites/{id}/arquivo   -> PDF (application/pdf)
///  - POST /api/v1/holerites (multipart)    -> 201 novo / 200 substituído { holerite, substituido }
/// O token vem da sessão na hora da chamada. O caminho do PDF no servidor nunca é exposto (RNF08).
class HoleriteRepositoryApi implements HoleriteRepository {
  HoleriteRepositoryApi(this._api, {required String? Function() token}) : _token = token;

  final ApiClient _api;
  final String? Function() _token;

  String _tokenObrigatorio() {
    final token = _token();
    if (token == null || token.isEmpty) throw const Falha('Sua sessão expirou. Entre novamente.');
    return token;
  }

  @override
  Future<List<Holerite>> listar({String? codigo, int? ano}) async {
    String? codigoOk;
    if (codigo != null && codigo.trim().isNotEmpty) {
      codigoOk = RegrasHolerite.normalizarCodigo(codigo);
      if (codigoOk == null) throw const Falha('Código inválido. Use o formato FUNC-0001.');
    }

    final itens = await _api.getLista(
      '/api/v1/holerites',
      token: _tokenObrigatorio(),
      query: {if (codigoOk != null) 'codigo': codigoOk, if (ano != null) 'ano': '$ano'},
    );
    return _protegido(() => [for (final j in itens) holeriteDeJson(j as Map<String, dynamic>)]);
  }

  @override
  Future<ArquivoHolerite> baixar(Holerite holerite) async {
    final bytes = await _api.getBytes('/api/v1/holerites/${holerite.id}/arquivo', token: _tokenObrigatorio());
    // Defesa: a API devolve PDF; qualquer outra coisa (página de erro de proxy, por exemplo) não é salva como holerite.
    if (!RegrasHolerite.pareceUmPdf(bytes)) {
      throw const ApiException('O servidor não devolveu um PDF válido. Procure o RH.');
    }
    return ArquivoHolerite(nome: holerite.nomeArquivo, bytes: bytes);
  }

  @override
  Future<HoleritePublicado> publicar({
    required String codigo,
    required int mes,
    required int ano,
    required String nomeArquivo,
    required Uint8List conteudo,
    bool substituir = false,
  }) async {
    // Tudo que dá para conferir sem rede é conferido antes de enviar até 5 MB.
    final codigoOk = RegrasHolerite.validarPublicacao(
      codigo: codigo,
      mes: mes,
      ano: ano,
      nomeArquivo: nomeArquivo,
      conteudo: conteudo,
    );

    final json = await _api.postMultipart(
      '/api/v1/holerites',
      campos: {
        'Codigo': codigoOk,
        'Mes': '$mes',
        'Ano': '$ano',
        if (substituir) 'Substituir': 'true',
      },
      campoArquivo: 'Arquivo',
      nomeArquivo: nomeArquivo,
      bytes: conteudo,
      token: _tokenObrigatorio(),
    );
    if (json == null) throw const ApiException('Resposta da API em formato inesperado.');

    return _protegido(() => HoleritePublicado(
          holerite: holeriteDeJson(json['holerite'] as Map<String, dynamic>),
          substituido: json['substituido'] as bool? ?? false,
        ));
  }

  /// Converte erro de leitura do JSON em mensagem para o usuário (a API mudou ou veio incompleta).
  static T _protegido<T>(T Function() leitura) {
    try {
      return leitura();
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  /// HoleriteDto -> [Holerite] (público e estático para os testes).
  static Holerite holeriteDeJson(Map<String, dynamic> j) => Holerite(
        id: (j['id'] as num).toInt(),
        codigoFuncionario: j['codigoFuncionario'] as String,
        nomeFuncionario: j['nomeFuncionario'] as String? ?? '',
        mes: (j['mes'] as num).toInt(),
        ano: (j['ano'] as num).toInt(),
        dataPublicacao: _lerData(j['dataPublicacao'] as String),
        nomeArquivo: j['nomeArquivo'] as String? ?? 'holerite.pdf',
      );

  /// "2026-10-21" -> DateTime(2026, 10, 21)
  static DateTime _lerData(String texto) {
    final m = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(texto);
    if (m == null) throw FormatException('Data inválida: $texto');
    return DateTime(int.parse(m.group(1)!), int.parse(m.group(2)!), int.parse(m.group(3)!));
  }
}
