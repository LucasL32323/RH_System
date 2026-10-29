/// Erro esperado de um fluxo do app, com mensagem pronta para exibir ao usuário.
/// As camadas de dados convertem qualquer problema (rede, API, regra) em uma [Falha];
/// a interface nunca mostra stack trace nem texto técnico.
class Falha implements Exception {
  const Falha(this.mensagem);

  final String mensagem;

  @override
  String toString() => 'Falha: $mensagem';
}
