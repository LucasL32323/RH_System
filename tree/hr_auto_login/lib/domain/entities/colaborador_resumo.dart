/// Identificação curta do colaborador nas tabelas: nome, código FUNC e setor.
class ColaboradorResumo {
  const ColaboradorResumo({required this.nome, required this.matricula, required this.departamento});

  final String nome;
  final String matricula; // FUNC-0142
  final String departamento;

  /// "Mariana Costa" -> "MC"
  String get iniciais => nome
      .trim()
      .split(RegExp(r'\s+'))
      .where((p) => p.isNotEmpty)
      .take(2)
      .map((p) => p[0].toUpperCase())
      .join();
}
