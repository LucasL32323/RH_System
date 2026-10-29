import '../../core/network/api_client.dart';
import '../models/status_sistema.dart';

/// Contrato do repositório (permite trocar por um fake nos testes).
abstract class SistemaRepository {
  Future<StatusSistema> obterStatus();
}

class SistemaRepositoryApi implements SistemaRepository {
  SistemaRepositoryApi(this._api);

  final ApiClient _api;

  @override
  Future<StatusSistema> obterStatus() async {
    final json = await _api.getJson('/api/v1/sistema/status');
    try {
      return StatusSistema.fromJson(json);
    } on TypeError {
      throw ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw ApiException('Resposta da API em formato inesperado.');
    }
  }
}
