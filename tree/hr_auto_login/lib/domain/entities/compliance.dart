/// Central de Compliance: leituras obrigatórias (AVISOS + AVISOS_VISUALIZACOES), testes obrigatórios
/// e de perfil (FORMULARIO + FORMULARIO_RESPOSTAS) e vitrine de benefícios (BENEFICIOS_FUNCIONARIOS).
class CentralCompliance {
  const CentralCompliance({
    required this.leituras,
    required this.testes,
    required this.perfilDisc,
    required this.beneficios,
  });

  final List<LeituraObrigatoria> leituras;
  final List<TesteObrigatorio> testes;
  final PerfilDisc perfilDisc;
  final List<Beneficio> beneficios;

  CentralCompliance copyWith({List<LeituraObrigatoria>? leituras}) => CentralCompliance(
        leituras: leituras ?? this.leituras,
        testes: testes,
        perfilDisc: perfilDisc,
        beneficios: beneficios,
      );
}

/// Aviso com data de expiração e confirmação de leitura (RF15).
class LeituraObrigatoria {
  const LeituraObrigatoria({
    required this.id,
    required this.titulo,
    required this.tipo,
    required this.publicadoEm,
    required this.expiraEm,
    required this.visualizacoes,
    required this.publico,
  });

  final String id;
  final String titulo;
  final String tipo; // Comunicado, Política, Norma
  final DateTime publicadoEm;
  final DateTime expiraEm;
  final int visualizacoes;
  final int publico;

  int get pendentes => publico - visualizacoes;
  double get adesao => publico == 0 ? 0 : visualizacoes / publico;
}

/// Formulário obrigatório (simulado de incêndio, aceite do código de conduta...) (RF18).
class TesteObrigatorio {
  const TesteObrigatorio({
    required this.titulo,
    required this.tipo,
    required this.perguntas,
    required this.respondidos,
    required this.publico,
    required this.prazo,
  });

  final String titulo;
  final String tipo;
  final int perguntas;
  final int respondidos;
  final int publico;
  final DateTime prazo;

  double get adesao => publico == 0 ? 0 : respondidos / publico;
}

/// Distribuição dos perfis DISC de quem já respondeu o teste de perfil.
class PerfilDisc {
  const PerfilDisc({
    required this.dominancia,
    required this.influencia,
    required this.estabilidade,
    required this.conformidade,
    required this.publico,
  });

  final int dominancia;
  final int influencia;
  final int estabilidade;
  final int conformidade;
  final int publico;

  int get respondidos => dominancia + influencia + estabilidade + conformidade;
}

class Beneficio {
  const Beneficio({required this.nome, required this.descricao, required this.aderentes, required this.publico});

  final String nome;
  final String descricao;
  final int aderentes;
  final int publico;
}
