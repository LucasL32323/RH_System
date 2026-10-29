import 'dart:typed_data';

import '../../core/errors/falha.dart';

/// Regras de holerite que o app confere ANTES de chamar a API (as mesmas do back-end, RNF17):
/// código FUNC válido, mês e ano possíveis, arquivo PDF não vazio e de até 5 MB.
/// O servidor repete todas as conferências e ainda valida a competência contra a data real e a admissão.
class RegrasHolerite {
  RegrasHolerite._();

  /// RNF17: no máximo 5 MB por arquivo.
  static const int tamanhoMaximoBytes = 5 * 1024 * 1024;

  static const int anoMinimo = 2000;

  static final RegExp _codigo = RegExp(r'^FUNC-(\d{4,})$');
  static final RegExp _soDigitos = RegExp(r'^\d{1,9}$');

  /// "func-0004" -> "FUNC-0004"; "4" -> "FUNC-0004". Devolve null se não der para formar um código válido.
  static String? normalizarCodigo(String? texto) {
    final t = (texto ?? '').trim().toUpperCase();
    if (_codigo.hasMatch(t)) return t;
    if (_soDigitos.hasMatch(t)) {
      final numero = int.parse(t);
      if (numero <= 0) return null;
      return 'FUNC-${numero.toString().padLeft(4, '0')}';
    }
    return null;
  }

  /// O arquivo começa com a assinatura de PDF ("%PDF-").
  static bool pareceUmPdf(Uint8List bytes) =>
      bytes.length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;

  /// Lança [Falha] com a mensagem para o usuário se o arquivo não puder ser publicado.
  static void validarArquivo(String nome, Uint8List bytes) {
    if (bytes.isEmpty) throw const Falha('O arquivo do holerite está vazio.');
    if (bytes.length > tamanhoMaximoBytes) {
      throw Falha('O arquivo do holerite deve ter no máximo ${tamanhoMaximoBytes ~/ (1024 * 1024)} MB.');
    }
    if (!nome.toLowerCase().endsWith('.pdf')) throw const Falha('O holerite deve ser um arquivo PDF.');
    if (!pareceUmPdf(bytes)) throw const Falha('O arquivo escolhido não é um PDF válido.');
  }

  /// Confere tudo da publicação e devolve o código normalizado ("FUNC-0004").
  static String validarPublicacao({
    required String codigo,
    required int mes,
    required int ano,
    required String nomeArquivo,
    required Uint8List conteudo,
  }) {
    final codigoOk = normalizarCodigo(codigo);
    if (codigoOk == null) throw const Falha('Código inválido. Use o formato FUNC-0001.');
    if (mes < 1 || mes > 12) throw const Falha('O mês do holerite deve estar entre 1 e 12.');
    if (ano < anoMinimo) throw const Falha('O ano do holerite deve ser $anoMinimo ou posterior.');
    validarArquivo(nomeArquivo, conteudo);
    return codigoOk;
  }

  /// "1,2 MB", "340 KB" ou "512 B" (para mostrar o tamanho do arquivo escolhido).
  static String tamanhoLegivel(int bytes) {
    if (bytes >= 1024 * 1024) return '${(bytes / (1024 * 1024)).toStringAsFixed(1).replaceAll('.', ',')} MB';
    if (bytes >= 1024) return '${(bytes / 1024).round()} KB';
    return '$bytes B';
  }
}
