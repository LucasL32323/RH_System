import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/app.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/holerite_repository_prototipo.dart';
import 'package:hr_auto/domain/entities/usuario_autenticado.dart';
import 'package:hr_auto/presentation/providers.dart';
import 'package:hr_auto/presentation/widgets/perfil/abas_perfil.dart';
import 'package:hr_auto/presentation/widgets/perfil/barra_superior_perfil.dart';

/// Áreas dos perfis Colaborador e RH: menu só com ícones, abas liberadas por perfil, Início,
/// navegação e saída. Repositórios em memória, sem rede (o app abre direto com a sessão simulada).
void main() {
  UsuarioAutenticado usuario(String perfil, {String nome = 'Ana Souza', String codigo = 'FUNC-0007'}) =>
      UsuarioAutenticado(
        token: 'token-de-teste',
        expiraEm: DateTime(2030),
        codigo: codigo,
        nome: nome,
        email: 'ana@hrauto.local',
        perfil: perfil,
      );

  Future<void> abrir(
    WidgetTester tester,
    UsuarioAutenticado u, {
    Size tamanho = const Size(420, 1400),
  }) async {
    tester.view.physicalSize = tamanho;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessaoProvider.overrideWith((ref) => u),
          adminRepositoryProvider.overrideWithValue(AdminRepositoryPrototipo(latencia: Duration.zero)),
          holeriteRepositoryProvider.overrideWithValue(
            HoleriteRepositoryPrototipo(
              codigoUsuario: u.codigo,
              nomeUsuario: u.nome,
              perfilUsuario: u.perfil,
              latencia: Duration.zero,
            ),
          ),
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

  Finder menu(AbaPerfil aba) => find.byKey(Key('aba_${aba.name}'));

  /// Título da aba atual na barra superior (o mesmo texto também aparece na página, por isso o filtro).
  Finder naBarra(String texto) =>
      find.descendant(of: find.byType(BarraSuperiorPerfil), matching: find.text(texto));

  group('AbaPerfil.doPerfil', () {
    test('Colaborador: Início, ponto, holerites e configurações', () {
      expect(AbaPerfil.doPerfil('Colaborador'), [
        AbaPerfil.inicio,
        AbaPerfil.ponto,
        AbaPerfil.holerites,
        AbaPerfil.configuracoes,
      ]);
    });

    test('RH ganha funcionários, novo funcionário e advertências', () {
      expect(AbaPerfil.doPerfil('RH'), AbaPerfil.values);
    });

    test('perfil desconhecido cai no menor conjunto', () {
      expect(AbaPerfil.doPerfil('Qualquer'), AbaPerfil.doPerfil('Colaborador'));
    });
  });

  group('Área do Colaborador', () {
    testWidgets('mostra só as 4 abas do perfil no menu de ícones', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      for (final aba in AbaPerfil.doPerfil('Colaborador')) {
        expect(find.byTooltip(aba.rotulo), findsOneWidget, reason: aba.rotulo);
      }
      expect(find.byTooltip('Funcionários'), findsNothing);
      expect(find.byTooltip('Novo funcionário'), findsNothing);
      expect(find.byTooltip('Gestão de advertências'), findsNothing);
      expect(find.byKey(const Key('atalho_funcionarios')), findsNothing);
    });

    testWidgets('Início saúda pelo primeiro nome e mostra o resumo do ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      expect(find.text('Olá, Ana!'), findsOneWidget);
      expect(find.text('Colaborador'), findsOneWidget);
      expect(find.text('FUNC-0007'), findsOneWidget);
      expect(find.text('Seu ponto hoje'), findsOneWidget);
      expect(find.text('Saldo do banco de horas'), findsOneWidget);
      expect(find.text('Atalhos'), findsOneWidget);
    });

    testWidgets('atalho do Início abre o Registro de ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byKey(const Key('atalho_ponto')));

      expect(naBarra('Registro de ponto'), findsOneWidget);
      expect(find.byKey(const Key('botao_bater_ponto')), findsOneWidget);
    });

    testWidgets('botão do resumo também abre o Registro de ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byKey(const Key('inicio_abrir_ponto')));

      expect(find.byKey(const Key('botao_bater_ponto')), findsOneWidget);
    });

    testWidgets('Holerites: só "Meus holerites", sem consulta da equipe nem publicação', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, menu(AbaPerfil.holerites));

      expect(find.text('Meus holerites'), findsOneWidget);
      expect(find.text('Holerites da equipe'), findsNothing);
      expect(find.text('Publicar holerite'), findsNothing);
    });

    testWidgets('Configurações mostra a conta e o canal com o RH; "Sair da conta" volta ao login', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, menu(AbaPerfil.configuracoes));
      expect(find.text('Minha conta'), findsOneWidget);
      expect(find.text('ana@hrauto.local'), findsOneWidget);
      expect(find.text('Canal de conversa com o RH'), findsOneWidget);
      // Estrutura da empresa é só do Admin.
      expect(find.text('Cargos'), findsNothing);

      await tocar(tester, find.byKey(const Key('configuracoes_sair')));
      expect(find.text('Bem-vindo de volta'), findsOneWidget);
    });

    testWidgets('o ícone de sair do menu também encerra a sessão', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byTooltip('Sair'));

      expect(find.text('Bem-vindo de volta'), findsOneWidget);
    });
  });

  group('Área do RH', () {
    testWidgets('mostra as 7 abas, com as de gestão de pessoas', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      for (final aba in AbaPerfil.values) {
        expect(find.byTooltip(aba.rotulo), findsOneWidget, reason: aba.rotulo);
      }
      expect(find.text('Olá, Bruno!'), findsOneWidget);
      expect(find.text('RH'), findsOneWidget);
      expect(find.byKey(const Key('atalho_funcionarios')), findsOneWidget);
    });

    testWidgets('abre Funcionários, Novo funcionário e Gestão de advertências pelo menu', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      await tocar(tester, menu(AbaPerfil.funcionarios));
      expect(naBarra('Funcionários'), findsOneWidget);

      await tocar(tester, menu(AbaPerfil.novoFuncionario));
      expect(naBarra('Novo funcionário'), findsOneWidget);

      await tocar(tester, menu(AbaPerfil.advertencias));
      expect(naBarra('Gestão de advertências'), findsOneWidget);
    });

    testWidgets('Holerites ganha consulta da equipe e publicação de PDF', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      await tocar(tester, menu(AbaPerfil.holerites));

      expect(find.text('Meus holerites'), findsOneWidget);
      expect(find.text('Holerites da equipe'), findsOneWidget);
      expect(find.text('Publicar holerite'), findsOneWidget);
    });
  });

  group('Raiz do app', () {
    testWidgets('no desktop a barra superior mostra a área e o menu continua à esquerda', (tester) async {
      await abrir(tester, usuario('Colaborador'), tamanho: const Size(1440, 900));

      expect(find.text('Área do colaborador'), findsOneWidget);
      expect(find.byTooltip('Holerites'), findsOneWidget);
      expect(find.text('Olá, Ana!'), findsOneWidget);
    });

    testWidgets('perfil desconhecido continua na tela provisória', (tester) async {
      await abrir(tester, usuario('Gestor'));

      expect(find.textContaining('Login validado'), findsOneWidget);
      expect(find.byKey(const Key('aba_inicio')), findsNothing);
    });
  });
}
