import 'dart:typed_data';

import '../entities/holerite.dart';

/// Contrato dos holerites (RF29 publicar, RF30 consultar e baixar). Duas implementações:
/// em memória (protótipo) e a da API (`HoleriteRepositoryApi`), escolhida em `providers.dart`.
abstract class HoleriteRepository {
  /// Holerites de quem está logado ([codigo] nulo) ou, só para RH/Admin, de outro funcionário
  /// pelo código FUNC. [ano] filtra por ano. Do mais recente para o mais antigo.
  Future<List<Holerite>> listar({String? codigo, int? ano});

  /// Baixa o PDF. O dono e o RH/Admin podem; o RH/Admin baixando o de outra pessoa gera auditoria no servidor.
  Future<ArquivoHolerite> baixar(Holerite holerite);

  /// Publica o PDF de um funcionário em um mês/ano (só RH/Admin; ninguém publica o próprio).
  /// Competência repetida só é aceita com [substituir] = true.
  Future<HoleritePublicado> publicar({
    required String codigo,
    required int mes,
    required int ano,
    required String nomeArquivo,
    required Uint8List conteudo,
    bool substituir = false,
  });
}
