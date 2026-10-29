/// Quem está logado. Espelha o LoginRespostaDto da API:
/// { "token": "...", "expiraEm": "2026-10-21T18:00:00-03:00",
///   "usuario": { "codigo": "FUNC-0001", "nome": "...", "email": "...", "perfil": "Admin" } }
class UsuarioAutenticado {
  const UsuarioAutenticado({
    required this.token,
    required this.expiraEm,
    required this.codigo,
    required this.nome,
    required this.email,
    required this.perfil,
  });

  final String token;
  final DateTime expiraEm;
  final String codigo; // FUNC-0001
  final String nome;
  final String email;
  final String perfil; // Colaborador, RH ou Admin

  /// Primeiro nome para a saudação ("Olá, Ana").
  String get primeiroNome => nome.trim().split(RegExp(r'\s+')).first;

  factory UsuarioAutenticado.fromJson(Map<String, dynamic> json) {
    final usuario = json['usuario'] as Map<String, dynamic>;
    return UsuarioAutenticado(
      token: json['token'] as String,
      expiraEm: DateTime.parse(json['expiraEm'] as String),
      codigo: usuario['codigo'] as String,
      nome: usuario['nome'] as String,
      email: usuario['email'] as String,
      perfil: usuario['perfil'] as String,
    );
  }
}
