import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/app.dart';
import 'package:hr_auto/core/config/app_config.dart';
import 'package:hr_auto/data/repositories/auth_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/presentation/providers.dart';

/// Testes de widget das telas de Login e Cadastro, com o repositório em memória (sem rede).
void main() {
  Future<void> abrirApp(WidgetTester tester, {Size tamanho = const Size(420, 1400)}) async {
    tester.view.physicalSize = tamanho;
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
  }

  Future<void> tocar(WidgetTester tester, Finder alvo) async {
    await tester.ensureVisible(alvo);
    await tester.pumpAndSettle();
    await tester.tap(alvo);
    await tester.pumpAndSettle();
  }

  Finder campo(String rotulo) => find.byKey(Key('campo_$rotulo'));

  testWidgets('abre no login com os campos pedidos', (tester) async {
    await abrirApp(tester);

    expect(find.text('Bem-vindo de volta'), findsOneWidget);
    expect(find.text('E-mail ou usuário'), findsOneWidget);
    expect(find.text('Senha'), findsOneWidget);
    expect(find.text('Esqueci minha senha'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Entrar'), findsOneWidget);
    expect(find.text('Não tem uma conta?'), findsOneWidget);
  });

  testWidgets('valida campos vazios no login', (tester) async {
    await abrirApp(tester);

    await tocar(tester, find.widgetWithText(FilledButton, 'Entrar'));

    expect(find.text('Informe o e-mail ou usuário.'), findsOneWidget);
    expect(find.text('Informe a senha.'), findsOneWidget);
  });

  testWidgets('alterna entre login e cadastro no mesmo card', (tester) async {
    await abrirApp(tester);

    await tocar(tester, find.text('Cadastre-se'));
    expect(find.text('Crie sua conta'), findsOneWidget);
    expect(find.text('Nome completo'), findsOneWidget);
    expect(find.text('Confirmação de senha'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Criar conta'), findsOneWidget);
    expect(find.text('Bem-vindo de volta'), findsNothing);

    await tocar(tester, find.widgetWithText(TextButton, 'Entrar'));
    expect(find.text('Bem-vindo de volta'), findsOneWidget);
  });

  testWidgets('login inválido mostra a mensagem genérica', (tester) async {
    await abrirApp(tester);

    await tester.enterText(campo('E-mail ou usuário'), 'ninguem@hrauto.local');
    await tester.enterText(campo('Senha'), 'Errada@123');
    await tocar(tester, find.widgetWithText(FilledButton, 'Entrar'));

    expect(find.text('E-mail ou senha inválidos.'), findsOneWidget);
  });

  testWidgets('login com a conta Admin abre a área do administrador e "Sair" volta', (tester) async {
    await abrirApp(tester);

    await tester.enterText(campo('E-mail ou usuário'), AppConfig.emailDemo);
    await tester.enterText(campo('Senha'), AppConfig.senhaDemo);
    await tocar(tester, find.widgetWithText(FilledButton, 'Entrar'));

    expect(find.text('Ajustes de ponto pendentes'), findsOneWidget);
    // No celular o menu de ícones continua à esquerda (RNF08: mesmo layout no celular e no computador).
    expect(find.byTooltip('Funcionários'), findsOneWidget);

    await tocar(tester, find.byTooltip('Sair'));
    expect(find.text('Bem-vindo de volta'), findsOneWidget);
  });

  testWidgets('cadastro válido volta ao login com o e-mail preenchido e permite entrar', (tester) async {
    await abrirApp(tester);
    await tocar(tester, find.text('Cadastre-se'));

    await tester.enterText(campo('Nome completo'), 'Ana Souza');
    await tester.enterText(campo('E-mail'), 'Ana@Empresa.com.br');
    await tester.enterText(campo('Senha'), 'Senha#Forte9');
    await tester.enterText(campo('Confirmação de senha'), 'Senha#Forte9');
    await tocar(tester, find.widgetWithText(FilledButton, 'Criar conta'));

    expect(find.text('Bem-vindo de volta'), findsOneWidget);
    expect(find.text('Conta criada! Entre com seu e-mail e senha.'), findsOneWidget);
    expect(find.text('ana@empresa.com.br'), findsOneWidget); // e-mail já preenchido

    await tester.enterText(campo('Senha'), 'Senha#Forte9');
    await tocar(tester, find.widgetWithText(FilledButton, 'Entrar'));
    expect(find.text('Olá, Ana!'), findsOneWidget);
    expect(find.text('Colaborador'), findsOneWidget);
  });

  testWidgets('cadastro valida senha fraca e confirmação diferente', (tester) async {
    await abrirApp(tester);
    await tocar(tester, find.text('Cadastre-se'));

    await tester.enterText(campo('Nome completo'), 'Ana Souza');
    await tester.enterText(campo('E-mail'), 'ana@empresa.com.br');
    await tester.enterText(campo('Senha'), 'fraca');
    await tester.enterText(campo('Confirmação de senha'), 'outra');
    await tocar(tester, find.widgetWithText(FilledButton, 'Criar conta'));

    expect(find.textContaining('no mínimo 8 caracteres'), findsOneWidget);
    expect(find.text('As senhas não conferem.'), findsOneWidget);
    expect(find.text('Crie sua conta'), findsOneWidget); // continua no cadastro
  });

  testWidgets('"Esqueci minha senha" abre o diálogo e confirma o envio', (tester) async {
    await abrirApp(tester);

    await tocar(tester, find.text('Esqueci minha senha'));
    expect(find.text('Recuperar senha'), findsOneWidget);

    await tester.enterText(
      find.descendant(of: find.byType(Dialog), matching: campo('E-mail')),
      'ana@empresa.com.br',
    );
    await tocar(tester, find.text('Enviar link'));

    expect(find.textContaining('Se o e-mail estiver cadastrado'), findsOneWidget);
    await tocar(tester, find.text('Voltar ao login'));
    expect(find.byType(Dialog), findsNothing);
  });

  testWidgets('no desktop mostra o painel da marca ao lado do card', (tester) async {
    await abrirApp(tester, tamanho: const Size(1440, 900));

    expect(find.text('Seu RH no\npiloto automático.'), findsOneWidget);
    expect(find.text('Bem-vindo de volta'), findsOneWidget);
  });
}
