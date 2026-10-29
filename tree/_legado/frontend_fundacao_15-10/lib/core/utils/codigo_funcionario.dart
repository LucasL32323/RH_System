/// Código do funcionário (FUNC-0001). Espelha a regra do back-end (RF11/RF13):
/// a busca é feita pelo código, nunca pelo nome.
class CodigoFuncionario {
  CodigoFuncionario._();

  static final RegExp _padrao = RegExp(r'^FUNC-(\d{4,9})$', caseSensitive: false);

  /// Gera FUNC-NNNN (mínimo de 4 dígitos) a partir do id.
  static String formatar(int id) {
    if (id <= 0) {
      throw ArgumentError.value(id, 'id', 'deve ser maior que zero');
    }
    return 'FUNC-${id.toString().padLeft(4, '0')}';
  }

  /// Lê o texto digitado na busca e devolve o id, ou null se o formato for inválido.
  static int? tentarConverter(String? texto) {
    if (texto == null) return null;
    final m = _padrao.firstMatch(texto.trim());
    if (m == null) return null;
    final id = int.parse(m.group(1)!);
    return id > 0 ? id : null;
  }
}
