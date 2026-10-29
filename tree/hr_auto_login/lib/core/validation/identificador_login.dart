/// Como o usuário se identifica no login: e-mail corporativo ou código do funcionário (FUNC-0001).
/// Espelha o back-end (AutenticarUsuario.cs): se tem "@" é e-mail; senão tem de ser um código.
/// A busca é sempre pelo código, nunca pelo nome (regra de negócio do HR-Auto).
enum TipoLogin { email, codigoFunc }

class IdentificadorLogin {
  const IdentificadorLogin._(this.tipo, this.valor);

  final TipoLogin tipo;

  /// E-mail sem espaços nas pontas, ou código normalizado ("func-1" -> "FUNC-0001").
  final String valor;

  static const int _tamanhoMaximoEmail = 254; // mesmo limite de EmailUsuario.cs

  // Mesmo padrão de EmailUsuario.cs (algo@dominio.ext).
  static final RegExp _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  // Aceita "FUNC-0001", "func-1", "FUNC1" e "FUNC 0001"; o código final sempre tem 4 dígitos.
  static final RegExp _codigo = RegExp(r'^FUNC[-\s]?(\d{1,4})$', caseSensitive: false);

  /// Interpreta o texto digitado. Devolve null se não for e-mail nem código válidos.
  static IdentificadorLogin? tentarLer(String? texto) {
    final limpo = (texto ?? '').trim();
    if (limpo.isEmpty) return null;

    if (limpo.contains('@')) {
      if (limpo.length > _tamanhoMaximoEmail || !_email.hasMatch(limpo)) return null;
      return IdentificadorLogin._(TipoLogin.email, limpo);
    }

    final m = _codigo.firstMatch(limpo);
    if (m == null) return null;
    final numero = int.parse(m.group(1)!);
    if (numero == 0) return null; // não existe funcionário 0
    return IdentificadorLogin._(TipoLogin.codigoFunc, formatarCodigo(numero));
  }

  /// 7 -> "FUNC-0007" (mesmo formato de CodigoFuncionario.Formatar no back-end).
  static String formatarCodigo(int id) => 'FUNC-${id.toString().padLeft(4, '0')}';

  bool get ehCodigo => tipo == TipoLogin.codigoFunc;
}
