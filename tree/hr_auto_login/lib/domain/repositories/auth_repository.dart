import '../entities/usuario_autenticado.dart';

/// Contrato de autenticação (camada de domínio).
/// Duas implementações em data/repositories:
///  - AuthRepositoryApi: conversa com a API .NET (Fase 4);
///  - AuthRepositoryPrototipo: em memória, para validar as telas sem back-end.
/// Toda falha esperada chega como Falha (core/errors), com mensagem pronta para o usuário.
abstract class AuthRepository {
  /// Login com e-mail (ou usuário) e senha. Devolve o usuário com o token JWT.
  Future<UsuarioAutenticado> entrar({required String login, required String senha});

  /// Cria a conta. Não faz login automático: o usuário volta para a tela de login.
  Future<void> cadastrar({required String nome, required String email, required String senha});

  /// "Esqueci minha senha". Devolve a mensagem para exibir (sempre a mesma, exista ou não a conta).
  Future<String> solicitarRecuperacaoSenha(String email);
}
