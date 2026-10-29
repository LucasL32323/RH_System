import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:file_saver/file_saver.dart';

import '../../core/errors/falha.dart';
import '../../core/files/arquivos.dart';

/// Seletor de PDF do sistema (pacote file_picker: web, Android, iOS e desktop).
/// É o único arquivo do app que conhece o plugin; o resto usa [SeletorArquivo].
class SeletorArquivoPlataforma implements SeletorArquivo {
  const SeletorArquivoPlataforma();

  @override
  Future<ArquivoSelecionado?> escolherPdf() async {
    FilePickerResult? resultado;
    try {
      // withData: true entrega os bytes também na web e no mobile (sem caminho de disco).
      resultado = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: const ['pdf'],
        withData: true,
      );
    } catch (_) {
      throw const Falha('Não foi possível abrir o seletor de arquivos neste aparelho.');
    }
    if (resultado == null || resultado.files.isEmpty) return null; // cancelou

    final arquivo = resultado.files.single;
    final Uint8List? bytes = arquivo.bytes;
    if (bytes == null) throw const Falha('Não foi possível ler o arquivo escolhido.');
    return ArquivoSelecionado(nome: arquivo.name, bytes: bytes);
  }
}

/// Entrega do PDF baixado (pacote file_saver): download no navegador ou gravação no aparelho.
class SalvadorArquivoPlataforma implements SalvadorArquivo {
  const SalvadorArquivoPlataforma();

  @override
  Future<String> salvarPdf({required String nome, required Uint8List bytes}) async {
    // O file_saver recebe o nome sem a extensão.
    final semExtensao = nome.toLowerCase().endsWith('.pdf') ? nome.substring(0, nome.length - 4) : nome;
    try {
      return await FileSaver.instance.saveFile(
        name: semExtensao,
        bytes: bytes,
        ext: 'pdf',
        mimeType: MimeType.pdf,
      );
    } catch (_) {
      throw const Falha('Não foi possível salvar o arquivo neste aparelho.');
    }
  }
}
