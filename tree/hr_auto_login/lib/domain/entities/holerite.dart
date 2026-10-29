import 'dart:typed_data';

/// Holerite (contracheque) de um funcionário em uma competência (mês/ano) — RF29 e RF30.
/// Espelha o HoleriteDto da API; o caminho do PDF no servidor nunca chega ao app (RNF08).
class Holerite {
  const Holerite({
    required this.id,
    required this.codigoFuncionario,
    required this.nomeFuncionario,
    required this.mes,
    required this.ano,
    required this.dataPublicacao,
    required this.nomeArquivo,
  });

  final int id;
  final String codigoFuncionario; // FUNC-0001
  final String nomeFuncionario;
  final int mes; // 1 a 12
  final int ano;
  final DateTime dataPublicacao;

  /// Nome sugerido ao baixar, sem dados pessoais (ex.: "holerite-2026-09.pdf").
  final String nomeArquivo;

  /// "09/2026"
  String get competencia => '${mes.toString().padLeft(2, '0')}/$ano';

  @override
  bool operator ==(Object other) =>
      other is Holerite && other.id == id && other.codigoFuncionario == codigoFuncionario && other.mes == mes && other.ano == ano;

  @override
  int get hashCode => Object.hash(id, codigoFuncionario, mes, ano);
}

/// PDF de um holerite já baixado da API, pronto para ser salvo no aparelho.
class ArquivoHolerite {
  const ArquivoHolerite({required this.nome, required this.bytes});

  final String nome;
  final Uint8List bytes;
}

/// Resultado da publicação: [substituido] é true quando o RH trocou o PDF de uma competência já publicada.
class HoleritePublicado {
  const HoleritePublicado({required this.holerite, required this.substituido});

  final Holerite holerite;
  final bool substituido;
}
