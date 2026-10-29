// Cadastros base da empresa (tabelas CARGO, SETORES, GESTORES e NIVEIS_DE_ACESSO).

/// Perfis de acesso (NIVEIS_DE_ACESSO), do menor para o maior.
enum NivelAcesso {
  colaborador('Colaborador', 'Acessa apenas os próprios dados: ponto, banco de horas, holerites e avisos.'),
  gestor('Gestor', 'Acesso do colaborador + equipe do seu setor; aprova ajustes de ponto e registra advertências.'),
  rh('RH', 'Acessa todos os colaboradores e publica holerites, avisos, treinamentos e formulários.'),
  admin('Admin', 'Acesso do RH + administração de níveis de acesso, cargos, setores e usuários.');

  const NivelAcesso(this.rotulo, this.descricao);
  final String rotulo;
  final String descricao;
}

/// Cargo com a carga horária semanal usada no cálculo de horas (CARGO.CARGA_HORARIA).
class Cargo {
  const Cargo({required this.nome, required this.cargaHorariaSemanal, this.descricao});

  final String nome;
  final double cargaHorariaSemanal;
  final String? descricao;

  /// Jornada diária considerando 5 dias por semana (44 h -> 8,8 h).
  double get cargaHorariaDiaria => cargaHorariaSemanal / 5;

  @override
  bool operator ==(Object other) => other is Cargo && other.nome == nome;

  @override
  int get hashCode => nome.hashCode;
}

/// Setor e o gestor que responde por ele (SETORES + GESTORES).
class Setor {
  const Setor({required this.nome, this.gestor, this.descricao});

  final String nome;
  final String? gestor; // nome do gestor responsável
  final String? descricao;

  @override
  bool operator ==(Object other) => other is Setor && other.nome == nome;

  @override
  int get hashCode => nome.hashCode;
}

class EstruturaEmpresa {
  const EstruturaEmpresa({required this.cargos, required this.setores});

  final List<Cargo> cargos;
  final List<Setor> setores;
}
