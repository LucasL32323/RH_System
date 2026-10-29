import '../../core/errors/falha.dart';
import '../../core/network/api_client.dart';
import '../../core/validation/identificador_login.dart';
import '../../domain/entities/usuario_autenticado.dart';
import '../../domain/repositories/auth_repository.dart';

/// Implementação real, sobre os endpoints da Fase 4:
///  - POST /api/v1/auth/login          -> 200 { token, expiraEm, usuario }
///  - POST /api/v1/auth/esqueci-senha  -> 202 { mensagem }
/// O login aceita o e-mail (campo "email") ou o código do funcionário (campo "login", ex.: FUNC-0001).
/// O cadastro ainda NÃO tem endpoint: no HR-Auto quem cria a conta do colaborador é o RH.
class AuthRepositoryApi implements AuthRepository {
  AuthRepositoryApi(this._api);

  final ApiClient _api;

  static const String _formatoInvalido = 'Entre com o e-mail corporativo ou com o código FUNC-0001.';

  @override
  Future<UsuarioAutenticado> entrar({required String login, required String senha}) async {
    // Texto fora do formato nunca existe no servidor: avisa antes de gastar uma tentativa
    // (o limite de tentativas do login conta cada chamada).
    final identificador = IdentificadorLogin.tentarLer(login);
    if (identificador == null) throw const Falha(_formatoInvalido);

    final json = await _api.postJson('/api/v1/auth/login', {
      // E-mail segue no campo "email" (compatível com a API antiga); código vai em "login".
      if (identificador.ehCodigo) 'login': identificador.valor else 'email': identificador.valor,
      'senha': senha,
    });
    if (json == null) throw const ApiException('Resposta da API em formato inesperado.');

    try {
      return UsuarioAutenticado.fromJson(json);
    } on TypeError {
      throw const ApiException('Resposta da API em formato inesperado.');
    } on FormatException {
      throw const ApiException('Resposta da API em formato inesperado.');
    }
  }

  @override
  Future<void> cadastrar({required String nome, required String email, required String senha}) async {
    // Sem endpoint público de autocadastro na API (decisão de segurança da Fase 4).
    // Quando o grupo definir o fluxo (ex.: convite do RH ou POST /api/v1/auth/cadastro), troque aqui.
    throw const Falha(
      'O autocadastro ainda não está disponível no servidor. Peça ao RH para criar sua conta.',
    );
  }

  @override
  Future<String> solicitarRecuperacaoSenha(String email) async {
    final json = await _api.postJson('/api/v1/auth/esqueci-senha', {'email': email.trim()});
    final mensagem = json?['mensagem'];
    return mensagem is String && mensagem.isNotEmpty
        ? mensagem
        : 'Se o e-mail estiver cadastrado, você receberá as instruções em instantes.';
  }
}
