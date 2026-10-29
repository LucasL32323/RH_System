import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/core/validation/validadores.dart';

/// As regras devem bater com as do back-end (EmailUsuario.cs e PoliticaSenha.cs).
void main() {
  group('E-mail', () {
    test('aceita e-mails válidos', () {
      expect(Validadores.email('ana@hrauto.local'), isNull);
      expect(Validadores.email('  ANA@HrAuto.Local '), isNull);
    });

    test('recusa vazio e formatos inválidos', () {
      expect(Validadores.email(''), 'Informe o e-mail.');
      for (final invalido in ['semarroba', '@semusuario.com', 'sem@dominio', 'dois@@arrobas.com', 'com espaco@x.com']) {
        expect(Validadores.email(invalido), 'E-mail inválido.', reason: invalido);
      }
    });

    test('recusa acima de 254 caracteres', () {
      expect(Validadores.email('${'a' * 250}@x.com'), 'E-mail inválido.');
    });
  });

  group('Login', () {
    test('aceita código FUNC e e-mail válido', () {
      expect(Validadores.loginUsuario('FUNC-0001'), isNull);
      expect(Validadores.loginUsuario('ana@hrauto.local'), isNull);
    });

    test('recusa usuário solto (a busca é pelo código)', () {
      expect(Validadores.loginUsuario('ana.souza'), 'Use o e-mail ou o código FUNC-0001.');
    });

    test('recusa vazio e e-mail malformado', () {
      expect(Validadores.loginUsuario('  '), 'Informe o e-mail ou usuário.');
      expect(Validadores.loginUsuario('ana@'), 'E-mail inválido.');
      expect(Validadores.senhaLogin(''), 'Informe a senha.');
    });
  });

  group('Nome completo', () {
    test('aceita nome e sobrenome com acentos, apóstrofo e hífen', () {
      expect(Validadores.nomeCompleto('Ana Souza'), isNull);
      expect(Validadores.nomeCompleto("João D'Ávila Pereira-Lima"), isNull);
      expect(Validadores.nomeCompleto('  Ana    Souza  '), isNull);
    });

    test('exige sobrenome e só letras', () {
      expect(Validadores.nomeCompleto(''), 'Informe o nome completo.');
      expect(Validadores.nomeCompleto('Ana'), 'Informe nome e sobrenome.');
      expect(Validadores.nomeCompleto('Ana S'), 'Informe nome e sobrenome.');
      expect(Validadores.nomeCompleto('Ana Souza 2'), 'Use apenas letras no nome.');
    });

    test('normaliza espaços', () {
      expect(Validadores.normalizarNome('  Ana   Paula  Souza '), 'Ana Paula Souza');
    });
  });

  group('Senha (PoliticaSenha)', () {
    test('aceita senhas que atendem a política', () {
      for (final senha in ['Admin@123', 'Senha#Forte9', 'Ação@2026a']) {
        expect(Validadores.senhaNova(senha), isNull, reason: senha);
      }
    });

    test('recusa senhas fracas', () {
      for (final senha in ['Ab1@', 'semmaiuscula1@', 'SEMMINUSCULA1@', 'SemNumero@@', 'SemSimbolo123', 'Com Espaco 1A']) {
        expect(Validadores.senhaNova(senha), contains('no mínimo 8 caracteres'), reason: senha);
      }
    });

    test('limite de 72 bytes (BCrypt)', () {
      expect(Validadores.senhaNova('Aa1@${'x' * 68}'), isNull); // exatamente 72 bytes
      expect(Validadores.senhaNova('Aa1@${'x' * 76}'), contains('muito longa'));
    });

    test('checklist reflete cada regra', () {
      final r = RequisitosSenha.avaliar('abc1');
      expect(r.tamanho, isFalse);
      expect(r.maiuscula, isFalse);
      expect(r.minuscula, isTrue);
      expect(r.numero, isTrue);
      expect(r.simbolo, isFalse);
      expect(r.pontuacao, 2);
      expect(RequisitosSenha.avaliar('Admin@123').atende, isTrue);
    });

    test('confirmação precisa ser igual', () {
      expect(Validadores.confirmacaoSenha('', 'Admin@123'), 'Confirme a senha.');
      expect(Validadores.confirmacaoSenha('Admin@124', 'Admin@123'), 'As senhas não conferem.');
      expect(Validadores.confirmacaoSenha('Admin@123', 'Admin@123'), isNull);
    });
  });
}
