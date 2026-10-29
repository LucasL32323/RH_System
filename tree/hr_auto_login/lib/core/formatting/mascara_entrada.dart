import 'package:flutter/services.dart';

/// Máscara de digitação: aceita só dígitos e encaixa no [padrao] ('#' = dígito).
///   MascaraEntrada('###.###.###-##')  -> CPF
///   MascaraEntrada('##/##/####')      -> data
class MascaraEntrada extends TextInputFormatter {
  MascaraEntrada(this.padrao) : _maximo = '#'.allMatches(padrao).length;

  static final cpf = MascaraEntrada('###.###.###-##');
  static final data = MascaraEntrada('##/##/####');
  static final cep = MascaraEntrada('#####-###');
  static final hora = MascaraEntrada('##:##');

  /// Celular com 9 dígitos; para fixo, a máscara se ajusta ao apagar o último dígito.
  static final telefone = MascaraEntrada('(##) #####-####');

  final String padrao;
  final int _maximo;

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final digitos = newValue.text.replaceAll(RegExp(r'\D'), '');
    final limitados = digitos.length > _maximo ? digitos.substring(0, _maximo) : digitos;

    final saida = StringBuffer();
    var i = 0;
    for (final c in padrao.split('')) {
      if (i >= limitados.length) break;
      if (c == '#') {
        saida.write(limitados[i++]);
      } else {
        saida.write(c);
      }
    }
    final texto = saida.toString();
    return TextEditingValue(text: texto, selection: TextSelection.collapsed(offset: texto.length));
  }
}
