/// Resposta de GET /api/v1/sistema/status.
class StatusSistema {
  const StatusSistema({
    required this.nome,
    required this.versao,
    required this.ambiente,
    required this.horaServidor,
  });

  final String nome;
  final String versao;
  final String ambiente;
  final DateTime horaServidor;

  factory StatusSistema.fromJson(Map<String, dynamic> json) => StatusSistema(
        nome: json['nome'] as String,
        versao: json['versao'] as String,
        ambiente: json['ambiente'] as String,
        horaServidor: DateTime.parse(json['horaServidor'] as String),
      );
}
