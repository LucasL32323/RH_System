import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/app.dart';
import 'package:hr_auto/core/config/app_config.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/core/validation/identificador_login.dart';
import 'package:hr_auto/core/validation/validadores.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/auth_repository_api.dart';
import 'package:hr_auto/data/repositories/auth_repository_prototipo.dart';
import 'package:hr_auto/presentation/providers.dart';

/// Login pelo código do funcionário (FUNC-0001): regras do formulário, corpo enviado à API
/// e fluxo completo no protótipo. A busca é sempre pelo código, nunca pelo nome.
void main() {
  group('IdentificadorLogin', () {
    test('e-mail é mantido (sem espaços nas pontas)', () {
      final id = IdentificadorLogin.tentarLer('  Ana@HrAuto.local ');
      expect(id, isNotNull);
      expect(id!.tipo, TipoLogin.email);
      expect(id.valor, 'Ana@HrAuto.local'); // a API normaliza para minúsculas
      expect(id.ehCodigo, isFalse);
    });

    test('código é normalizado para FUNC-0000', () {
      for (final digitado in ['FUNC-0007', 'func-0007', 'Func-7', 'FUNC7', 'func 7', '  FUNC-07 ']) {
        final id = IdentificadorLogin.tentarLer(digitado);
        expect(id?.valor, 'FUNC-0007', reason: digitado);
        expect(id?.tipo, TipoLogin.codigoFunc, reason: digitado);
      }
    });

    test('recusa o que não é e-mail nem código', () {
      for (final invalido in [
        null,
        '',
        '   ',
        'ana.souza', // usuário/nome não vale: a busca é pelo código
        'Ana Souza',
        'FUNC-0000', // não existe funcionário 0
        'FUNC-12345', // mais de 4 dígitos
        'FUNC-',
        '0007',
        'ana@',
        'sem@dominio',
        'dois@@arrobas.com',
      ]) {
        expect(IdentificadorLogin.tentarLer(invalido), isNull, reason: '$invalido');
      }
    });

    test('e-mail acima de 254 caracteres é recusado', () {
      expect(IdentificadorLogin.tentarLer('${'a' * 250}@x.com'), isNull);
    });

    test('formatarCodigo completa com zeros', () {
      expect(IdentificadorLogin.formatarCodigo(1), 'FUNC-0001');
      expect(IdentificadorLogin.formatarCodigo(1234), 'FUNC-1234');
    });
  });

  group('Validadores.loginUsuario', () {
    test('aceita e-mail e código', () {
      expect(Validadores.loginUsuario('ana@hrauto.local'), isNull);
      expect(Validadores.loginUsuario('FUNC-0001'), isNull);
      expect(Validadores.loginUsuario('func-1'), isNull);
    });

    test('mensagens de erro', () {
      expect(Validadores.loginUsuario('  '), 'Informe o e-mail ou usuário.');
      expect(Validadores.loginUsuario('ana@'), 'E-mail inválido.');
      expect(Validadores.loginUsuario('ana.souza'), 'Use o e-mail ou o código FUNC-0001.');
      expect(Validadores.loginUsuario('FUNC-0000'), 'Use o e-mail ou o código FUNC-0001.');
    });
  });

  group('AuthRepositoryApi (login pelo código)', () {
    http.Response resposta(Object corpo, [int status = 200]) => http.Response(
          jsonEncode(corpo),
          status,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );

    final sucesso = {
      'token': 'jwt-de-teste',
      'expiraEm': '2026-10-28T18:30:00-03:00',
      'usuario': {'codigo': 'FUNC-0007', 'nome': 'Ana Souza', 'email': 'ana@hrauto.local', 'perfil': 'RH'},
    };

    test('código vai no campo "login", já normalizado', () async {
      late Map<String, dynamic> corpo;
      final cliente = MockClient((req) async {
        corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return resposta(sucesso);
      });
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      final usuario = await repo.entrar(login: ' func-7 ', senha: 'Senha@123');

      expect(corpo, {'login': 'FUNC-0007', 'senha': 'Senha@123'});
      expect(usuario.codigo, 'FUNC-0007');
      expect(usuario.perfil, 'RH');
      expect(usuario.token, 'jwt-de-teste');
    });

    test('e-mail continua no campo "email"', () async {
      late Map<String, dynamic> corpo;
      final cliente = MockClient((req) async {
        corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return resposta(sucesso);
      });
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      await repo.entrar(login: ' ana@hrauto.local ', senha: 'Senha@123');

      expect(corpo, {'email': 'ana@hrauto.local', 'senha': 'Senha@123'});
    });

    test('texto fora do formato não chama a API (poupa o limite de tentativas)', () async {
      var chamou = false;
      final cliente = MockClient((_) async {
        chamou = true;
        return http.Response('', 401);
      });
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      await expectLater(
        repo.entrar(login: 'Ana Souza', senha: 'Senha@123'),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('código FUNC-0001'))),
      );
      expect(chamou, isFalse);
    });

    test('código com senha errada mostra a mensagem genérica do servidor', () async {
      final cliente = MockClient((_) async => http.Response('', 401));
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      await expectLater(
        repo.entrar(login: 'FUNC-0007', senha: 'Errada@123'),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', 'E-mail ou senha inválidos.')),
      );
    });
  });

  group('AuthRepositoryPrototipo (login pelo código)', () {
    final repo = AuthRepositoryPrototipo(latencia: Duration.zero);

    test('o admin de demonstração entra por FUNC-0001, em qualquer caixa', () async {
      final usuario = await repo.entrar(login: 'func-1', senha: AppConfig.senhaDemo);

      expect(usuario.codigo, 'FUNC-0001');
      expect(usuario.perfil, 'Admin');
    });

    test('senha errada, código inexistente e nome recebem a mesma mensagem', () async {
      for (final login in ['FUNC-0001', 'FUNC-0099', 'Administrador do Sistema']) {
        final senha = login == 'FUNC-0001' ? 'Errada@123' : AppConfig.senhaDemo;
        await expectLater(
          repo.entrar(login: login, senha: senha),
          throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'E-mail ou senha inválidos.')),
          reason: login,
        );
      }
    });

    test('conta criada no cadastro também entra pelo código', () async {
      await repo.cadastrar(nome: 'Ana Souza', email: 'ana@empresa.com.br', senha: 'Senha#Forte9');

      final usuario = await repo.entrar(login: 'FUNC-0002', senha: 'Senha#Forte9');

      expect(usuario.email, 'ana@empresa.com.br');
      expect(usuario.perfil, 'Colaborador');
    });
  });

  testWidgets('tela de login: entra com o código FUNC e abre a área do Admin', (tester) async {
    tester.view.physicalSize = const Size(420, 1400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(AuthRepositoryPrototipo(latencia: Duration.zero)),
          adminRepositoryProvider.overrideWithValue(AdminRepositoryPrototipo(latencia: Duration.zero)),
        ],
        child: const HrAutoApp(usarGoogleFonts: false),
      ),
    );
    await tester.pumpAndSettle();

    // Texto fora do formato é barrado no formulário.
    await tester.enterText(find.byKey(const Key('campo_E-mail ou usuário')), 'administrador');
    await tester.enterText(find.byKey(const Key('campo_Senha')), AppConfig.senhaDemo);
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pumpAndSettle();
    expect(find.text('Use o e-mail ou o código FUNC-0001.'), findsOneWidget);

    // Com o código, entra.
    await tester.enterText(find.byKey(const Key('campo_E-mail ou usuário')), 'func-0001');
    await tester.enterText(find.byKey(const Key('campo_Senha')), AppConfig.senhaDemo);
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Ajustes de ponto pendentes'), findsOneWidget);
  });
}
