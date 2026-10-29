import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/app_config.dart';

/// Erro de comunicação com a API, com mensagem pronta para exibir ao usuário.
class ApiException implements Exception {
  ApiException(this.mensagem, {this.statusCode});

  final String mensagem;
  final int? statusCode;

  @override
  String toString() => 'ApiException($statusCode): $mensagem';
}

/// Cliente HTTP único do app. Na Fase 4/5 passa a enviar o token JWT no cabeçalho.
class ApiClient {
  ApiClient({http.Client? cliente, String? baseUrl})
      : _cliente = cliente ?? http.Client(),
        _baseUrl = baseUrl ?? AppConfig.apiBaseUrl;

  final http.Client _cliente;
  final String _baseUrl;

  /// GET que devolve o JSON (objeto) da resposta.
  Future<Map<String, dynamic>> getJson(String caminho) async {
    final uri = Uri.parse('$_baseUrl$caminho');
    try {
      final resposta = await _cliente
          .get(uri, headers: {'Accept': 'application/json'})
          .timeout(AppConfig.timeoutApi);

      if (resposta.statusCode < 200 || resposta.statusCode >= 300) {
        throw ApiException(
          'A API respondeu com erro ${resposta.statusCode}.',
          statusCode: resposta.statusCode,
        );
      }

      final corpo = jsonDecode(utf8.decode(resposta.bodyBytes));
      if (corpo is! Map<String, dynamic>) {
        throw ApiException('Resposta da API em formato inesperado.');
      }
      return corpo;
    } on TimeoutException {
      throw ApiException('A API demorou demais para responder.');
    } on http.ClientException {
      throw ApiException('Não foi possível conectar à API.');
    } on FormatException {
      throw ApiException('Resposta da API em formato inesperado.');
    }
  }

  void dispose() => _cliente.close();
}
