import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart' show MediaType;

import '../config/app_config.dart';
import '../errors/falha.dart';

/// Erro de comunicação com a API. Herda de [Falha]: a mensagem já serve para o usuário.
class ApiException extends Falha {
  const ApiException(super.mensagem, {this.statusCode});

  final int? statusCode;

  @override
  String toString() => 'ApiException($statusCode): $mensagem';
}

/// Cliente HTTP único do app.
/// A API .NET responde erros em ProblemDetails (application/problem+json): o campo
/// "detail" já vem em português e é o que mostramos ao usuário.
class ApiClient {
  ApiClient({http.Client? cliente, String? baseUrl})
      : _cliente = cliente ?? http.Client(),
        _baseUrl = baseUrl ?? AppConfig.apiBaseUrl;

  final http.Client _cliente;
  final String _baseUrl;

  /// Tempo máximo para enviar ou baixar um arquivo (PDF de até 5 MB, RNF17).
  /// É maior que o [AppConfig.timeoutApi] das chamadas JSON.
  static const Duration timeoutArquivos = Duration(seconds: 60);

  /// POST com corpo JSON. Devolve o objeto JSON da resposta, ou null quando não há corpo (204).
  Future<Map<String, dynamic>?> postJson(
    String caminho,
    Map<String, dynamic> corpo, {
    String? token,
  }) async =>
      _comoObjeto(await _enviar('POST', caminho, corpo: corpo, token: token));

  /// PUT com corpo JSON (atualização completa). Devolve o objeto da resposta ou null (204).
  Future<Map<String, dynamic>?> putJson(
    String caminho,
    Map<String, dynamic> corpo, {
    String? token,
  }) async =>
      _comoObjeto(await _enviar('PUT', caminho, corpo: corpo, token: token));

  /// PATCH com corpo JSON (atualização parcial). Devolve o objeto da resposta ou null (204).
  Future<Map<String, dynamic>?> patchJson(
    String caminho,
    Map<String, dynamic> corpo, {
    String? token,
  }) async =>
      _comoObjeto(await _enviar('PATCH', caminho, corpo: corpo, token: token));

  /// DELETE. A API responde 204 sem corpo.
  Future<void> delete(String caminho, {String? token}) async {
    await _enviar('DELETE', caminho, token: token);
  }

  /// GET que espera um objeto JSON na resposta. [query] vira a query string (?de=...&ate=...).
  Future<Map<String, dynamic>> getJson(
    String caminho, {
    String? token,
    Map<String, String>? query,
  }) async {
    final json = _comoObjeto(await _enviar('GET', caminho, token: token, query: query));
    if (json == null) throw const ApiException('Resposta da API em formato inesperado.');
    return json;
  }

  /// GET que espera um array JSON na resposta (ex.: /cargos, /departamentos e /holerites).
  Future<List<dynamic>> getLista(
    String caminho, {
    String? token,
    Map<String, String>? query,
  }) async {
    final json = await _enviar('GET', caminho, token: token, query: query);
    if (json is! List) throw const ApiException('Resposta da API em formato inesperado.');
    return json;
  }

  /// GET de um arquivo (ex.: PDF do holerite). Devolve os bytes da resposta sem interpretá-los.
  /// Erros chegam como [ApiException] com a mensagem do ProblemDetails (que vem em JSON, não em binário).
  Future<Uint8List> getBytes(
    String caminho, {
    String? token,
    Map<String, String>? query,
  }) =>
      _protegido(() async {
        final resposta = await _cliente
            .get(_uri(caminho, query), headers: _cabecalhos(token: token, aceita: 'application/pdf, application/json'))
            .timeout(timeoutArquivos);

        if (!_sucesso(resposta.statusCode)) {
          throw ApiException(
            _mensagemDeErro(
              resposta.statusCode,
              utf8.decode(resposta.bodyBytes, allowMalformed: true),
              autenticado: token != null,
            ),
            statusCode: resposta.statusCode,
          );
        }
        return resposta.bodyBytes;
      });

  /// POST multipart/form-data com UM arquivo e campos de texto (ex.: publicar holerite).
  /// O Content-Type (com o boundary) é montado pelo próprio pacote http. Devolve o objeto JSON da resposta
  /// ou null (204).
  Future<Map<String, dynamic>?> postMultipart(
    String caminho, {
    required Map<String, String> campos,
    required String campoArquivo,
    required String nomeArquivo,
    required List<int> bytes,
    String tipoConteudo = 'application/pdf',
    String? token,
  }) async {
    final json = await _protegido(() async {
      final requisicao = http.MultipartRequest('POST', _uri(caminho))
        ..headers.addAll(_cabecalhos(token: token))
        ..fields.addAll(campos)
        ..files.add(http.MultipartFile.fromBytes(
          campoArquivo,
          bytes,
          filename: nomeArquivo,
          contentType: MediaType.parse(tipoConteudo),
        ));

      final resposta = await (() async {
        final enviada = await _cliente.send(requisicao);
        return http.Response.fromStream(enviada);
      })()
          .timeout(timeoutArquivos);

      return _lerJson(resposta, autenticado: token != null);
    });
    return _comoObjeto(json);
  }

  /// Aceita só objeto JSON (ou vazio). Qualquer outra forma é resposta inesperada.
  static Map<String, dynamic>? _comoObjeto(Object? json) {
    if (json == null) return null;
    if (json is Map<String, dynamic>) return json;
    throw const ApiException('Resposta da API em formato inesperado.');
  }

  static bool _sucesso(int status) => status >= 200 && status < 300;

  Uri _uri(String caminho, [Map<String, String>? query]) {
    final base = Uri.parse('$_baseUrl$caminho');
    return (query == null || query.isEmpty)
        ? base
        : base.replace(queryParameters: {...base.queryParameters, ...query});
  }

  static Map<String, String> _cabecalhos({String? token, bool corpoJson = false, String aceita = 'application/json'}) => {
        'Accept': aceita,
        if (corpoJson) 'Content-Type': 'application/json; charset=utf-8',
        if (token != null) 'Authorization': 'Bearer $token',
      };

  /// Converte as falhas de rede em [ApiException] com mensagem para o usuário.
  Future<T> _protegido<T>(Future<T> Function() acao) async {
    try {
      return await acao();
    } on ApiException {
      rethrow;
    } on TimeoutException {
      throw const ApiException('O servidor demorou demais para responder. Tente novamente.');
    } on http.ClientException {
      throw const ApiException('Não foi possível conectar ao servidor. Verifique sua conexão.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  /// Confere o status e decodifica o JSON (objeto ou array); null se o corpo vier vazio.
  static Object? _lerJson(http.Response resposta, {required bool autenticado}) {
    final conteudo = utf8.decode(resposta.bodyBytes);

    if (!_sucesso(resposta.statusCode)) {
      throw ApiException(
        _mensagemDeErro(resposta.statusCode, conteudo, autenticado: autenticado),
        statusCode: resposta.statusCode,
      );
    }

    if (conteudo.trim().isEmpty) return null;

    final json = jsonDecode(conteudo);
    if (json is! Map<String, dynamic> && json is! List) {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
    return json;
  }

  /// Envia a requisição JSON e devolve o JSON decodificado (objeto ou array), ou null se o corpo vier vazio.
  Future<Object?> _enviar(
    String metodo,
    String caminho, {
    Map<String, dynamic>? corpo,
    String? token,
    Map<String, String>? query,
  }) =>
      _protegido(() async {
        final uri = _uri(caminho, query);
        final temCorpo = metodo == 'POST' || metodo == 'PUT' || metodo == 'PATCH';
        final cabecalhos = _cabecalhos(token: token, corpoJson: temCorpo);
        final texto = jsonEncode(corpo ?? const <String, dynamic>{});

        final requisicao = switch (metodo) {
          'GET' => _cliente.get(uri, headers: cabecalhos),
          'POST' => _cliente.post(uri, headers: cabecalhos, body: texto),
          'PUT' => _cliente.put(uri, headers: cabecalhos, body: texto),
          'PATCH' => _cliente.patch(uri, headers: cabecalhos, body: texto),
          'DELETE' => _cliente.delete(uri, headers: cabecalhos),
          _ => throw ArgumentError.value(metodo, 'metodo', 'Método HTTP não suportado'),
        };
        final resposta = await requisicao.timeout(AppConfig.timeoutApi);
        return _lerJson(resposta, autenticado: token != null);
      });

  /// Usa o "detail" do ProblemDetails; sem ele, uma mensagem padrão por status.
  /// [autenticado] = a chamada levava token: um 401 aí significa sessão expirada, não senha errada.
  static String _mensagemDeErro(int status, String corpo, {required bool autenticado}) {
    try {
      final json = jsonDecode(corpo);
      if (json is Map<String, dynamic>) {
        final detalhe = json['detail'] ?? json['title'];
        if (detalhe is String && detalhe.trim().isNotEmpty) return detalhe;
      }
    } on FormatException {
      // Corpo vazio ou não-JSON: cai na mensagem padrão.
    }

    return switch (status) {
      401 when autenticado => 'Sua sessão expirou. Entre novamente.',
      401 => 'E-mail ou senha inválidos.',
      403 => 'Você não tem permissão para esta operação.',
      413 => 'O arquivo enviado é grande demais.',
      429 => 'Muitas tentativas em pouco tempo. Aguarde um instante e tente de novo.',
      >= 500 => 'O servidor está indisponível. Tente novamente em instantes.',
      _ => 'Não foi possível concluir a operação (erro $status).',
    };
  }

  void dispose() => _cliente.close();
}
