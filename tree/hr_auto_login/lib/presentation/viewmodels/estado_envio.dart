/// Estado de um formulário que envia dados (login, cadastro, recuperação de senha).
class EstadoEnvio {
  const EstadoEnvio({this.carregando = false, this.erro, this.sucesso});

  const EstadoEnvio.carregando() : this(carregando: true);

  /// true enquanto a requisição está em andamento (botão mostra o spinner e fica bloqueado).
  final bool carregando;

  /// Mensagem de erro para o banner do formulário.
  final String? erro;

  /// Mensagem de sucesso (usada na recuperação de senha).
  final String? sucesso;
}
