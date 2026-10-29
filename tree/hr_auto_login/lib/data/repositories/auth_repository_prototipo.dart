import '../../core/config/app_config.dart';
import '../../core/errors/falha.dart';
import '../../core/validation/identificador_login.dart';
import '../../domain/entities/usuario_autenticado.dart';
import '../../domain/repositories/auth_repository.dart';

/// Repositório em memória para validar as telas sem subir a API.
/// Simula o comportamento do back-end: mesma conta de teste, mesma mensagem genérica
/// para credencial inválida, login por e-mail ou por código FUNC e mesma resposta do
/// "esqueci minha senha". Os dados somem ao fechar o app (nada é gravado em disco).
class AuthRepositoryPrototipo implements AuthRepository {
  AuthRepositoryPrototipo({this.latencia = const Duration(milliseconds: 700)}) {
    _contas[AppConfig.emailDemo] = const _Conta(
      id: 1,
      nome: 'Administrador do Sistema',
      email: AppConfig.emailDemo,
      senha: AppConfig.senhaDemo,
      perfil: 'Admin',
    );
  }

  /// Atraso artificial para ver os estados de carregamento (zero nos testes).
  final Duration latencia;

  final Map<String, _Conta> _contas = {};

  static const String _credenciaisInvalidas = 'E-mail ou senha inválidos.';

  @override
  Future<UsuarioAutenticado> entrar({required String login, required String senha}) async {
    await Future<void>.delayed(latencia);

    final conta = _buscarConta(login);
    if (conta == null || conta.senha != senha) throw const Falha(_credenciaisInvalidas);

    return UsuarioAutenticado(
      token: 'prototipo-${DateTime.now().millisecondsSinceEpoch}',
      expiraEm: DateTime.now().add(const Duration(hours: 8)),
      codigo: IdentificadorLogin.formatarCodigo(conta.id),
      nome: conta.nome,
      email: conta.email,
      perfil: conta.perfil,
    );
  }

  /// Procura pelo e-mail ou pelo código FUNC (nunca pelo nome), como a API.
  _Conta? _buscarConta(String login) {
    final identificador = IdentificadorLogin.tentarLer(login);
    if (identificador == null) return null;

    if (!identificador.ehCodigo) return _contas[identificador.valor.toLowerCase()];

    for (final conta in _contas.values) {
      if (IdentificadorLogin.formatarCodigo(conta.id) == identificador.valor) return conta;
    }
    return null;
  }

  @override
  Future<void> cadastrar({required String nome, required String email, required String senha}) async {
    await Future<void>.delayed(latencia);

    final chave = email.trim().toLowerCase();
    if (_contas.containsKey(chave)) throw const Falha('Este e-mail já está cadastrado.');

    _contas[chave] = _Conta(
      id: _contas.length + 1,
      nome: nome,
      email: chave,
      senha: senha,
      perfil: 'Colaborador', // perfil inicial; RH/Admin são atribuídos pelo administrador
    );
  }

  @override
  Future<String> solicitarRecuperacaoSenha(String email) async {
    await Future<void>.delayed(latencia);
    return 'Se o e-mail estiver cadastrado, você receberá as instruções em instantes.';
  }
}

class _Conta {
  const _Conta({
    required this.id,
    required this.nome,
    required this.email,
    required this.senha,
    required this.perfil,
  });

  final int id;
  final String nome;
  final String email;
  final String senha; // em memória e só no protótipo; a API real guarda hash BCrypt
  final String perfil;
}
