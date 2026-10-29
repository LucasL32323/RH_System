import 'dart:typed_data';

/// Arquivo escolhido pelo usuário no aparelho (já lido para a memória).
class ArquivoSelecionado {
  const ArquivoSelecionado({required this.nome, required this.bytes});

  final String nome;
  final Uint8List bytes;

  int get tamanho => bytes.length;
}

/// Abre o seletor de arquivos do sistema. Abstração para os ViewModels e os testes não dependerem do plugin.
abstract class SeletorArquivo {
  /// Pede um PDF ao usuário. Devolve null se ele cancelar; lança `Falha` se não conseguir ler o arquivo.
  Future<ArquivoSelecionado?> escolherPdf();
}

/// Entrega um arquivo ao usuário: download no navegador (web) ou gravação/compartilhamento no aparelho (mobile).
abstract class SalvadorArquivo {
  /// Salva [bytes] com o [nome] (ex.: "holerite-2026-09.pdf"). Devolve onde ficou (caminho ou o próprio nome).
  Future<String> salvarPdf({required String nome, required Uint8List bytes});
}
