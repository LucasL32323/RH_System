import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/core/utils/codigo_funcionario.dart';

void main() {
  group('CodigoFuncionario.formatar', () {
    test('usa no mínimo 4 dígitos', () {
      expect(CodigoFuncionario.formatar(1), 'FUNC-0001');
      expect(CodigoFuncionario.formatar(42), 'FUNC-0042');
      expect(CodigoFuncionario.formatar(12345), 'FUNC-12345');
    });

    test('rejeita id zero ou negativo', () {
      expect(() => CodigoFuncionario.formatar(0), throwsArgumentError);
      expect(() => CodigoFuncionario.formatar(-1), throwsArgumentError);
    });
  });

  group('CodigoFuncionario.tentarConverter', () {
    test('aceita códigos válidos (minúsculas e espaços)', () {
      expect(CodigoFuncionario.tentarConverter('FUNC-0001'), 1);
      expect(CodigoFuncionario.tentarConverter(' func-0123 '), 123);
    });

    test('rejeita nomes e formatos inválidos', () {
      expect(CodigoFuncionario.tentarConverter(null), isNull);
      expect(CodigoFuncionario.tentarConverter(''), isNull);
      expect(CodigoFuncionario.tentarConverter('Maria'), isNull);
      expect(CodigoFuncionario.tentarConverter('FUNC-1'), isNull);
      expect(CodigoFuncionario.tentarConverter('FUNC-0000'), isNull);
    });
  });
}
