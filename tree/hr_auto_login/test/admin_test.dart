import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/app.dart';
import 'package:hr_auto/core/config/app_config.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/auth_repository_prototipo.dart';
import 'package:hr_auto/presentation/providers.dart';

/// Testes de widget da área do Administrador (repositórios em memória, relógio fixo, sem rede).
void main() {
  // Quarta-feira: segunda e terça já têm batidas no espelho; hoje começa vazio.
  final agora = DateTime(2026, 10, 21, 10, 30);

  Future<void> entrarComoAdmin(WidgetTester tester, {Size tamanho = const Size(1440, 1000)}) async {
    tester.view.physicalSize = tamanho;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(AuthRepositoryPrototipo(latencia: Duration.zero)),
          adminRepositoryProvider.overrideWithValue(AdminRepositoryPrototipo(latencia: Duration.zero, relogio: () => agora)),
        ],
        child: const HrAutoApp(usarGoogleFonts: false),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('campo_E-mail ou usuário')), AppConfig.emailDemo);
    await tester.enterText(find.byKey(const Key('campo_Senha')), AppConfig.senhaDemo);
    await tester.tap(find.widgetWithText(FilledButton, 'Entrar'));
    await tester.pumpAndSettle();
  }

  Future<void> tocar(WidgetTester tester, Finder alvo) async {
    await tester.ensureVisible(alvo);
    await tester.pumpAndSettle();
    await tester.tap(alvo);
    await tester.pumpAndSettle();
  }

  Future<void> abrirAba(WidgetTester tester, String nome) => tocar(tester, find.byKey(Key('aba_$nome')));

  Future<void> escolher(WidgetTester tester, String campo, String opcao) async {
    await tocar(tester, find.byKey(Key('campo_$campo')));
    await tocar(tester, find.text(opcao).last);
  }

  Future<void> digitar(WidgetTester tester, String campo, String texto) async {
    final alvo = find.byKey(Key('campo_$campo'));
    await tester.ensureVisible(alvo);
    await tester.enterText(alvo, texto);
  }

  testWidgets('menu só com ícones e dashboard com os indicadores do perfil', (tester) async {
    await entrarComoAdmin(tester);

    // RNF08: uma aba por ícone, com o nome no tooltip.
    for (final aba in [
      'Dashboard', 'Registro de ponto', 'Funcionários', 'Novo funcionário',
      'Central de Compliance', 'Gestão de advertências', 'Configurações',
    ]) {
      expect(find.byTooltip(aba), findsOneWidget);
    }

    // KPIs (RF21).
    expect(find.text('248'), findsOneWidget);
    expect(find.text('Absenteísmo'), findsOneWidget);
    expect(find.text('2,8%'), findsOneWidget);
    expect(find.text('312 h'), findsOneWidget);
    expect(find.text('Ajustes pendentes'), findsOneWidget);

    // Painéis.
    expect(find.text('Ajustes de ponto pendentes'), findsOneWidget);
    expect(find.text('Atrasos x horas extras'), findsOneWidget);
    expect(find.text('Termômetro de clima'), findsOneWidget);
    expect(find.text('Banco de horas'), findsOneWidget);

    // Semáforo calculado das advertências: 4 no amarelo, 2 no vermelho, o resto no verde.
    expect(find.text('Semáforo de advertências'), findsOneWidget);
    expect(find.text('242'), findsOneWidget);
    expect(find.text('3 colaboradores passaram de 2 h extras ontem'), findsOneWidget);
  });

  testWidgets('aprovar ajuste de ponto atualiza a linha e "Desfazer" volta para pendente', (tester) async {
    await entrarComoAdmin(tester);

    expect(find.text('Aprovar'), findsNWidgets(5));
    await tocar(tester, find.text('Aprovar').first);
    expect(find.text('Aprovado'), findsOneWidget);
    expect(find.text('Aprovar'), findsNWidgets(4));
    expect(find.text('Ajuste de Rafael Nogueira: aprovado.'), findsOneWidget);

    await tocar(tester, find.widgetWithText(SnackBarAction, 'Desfazer'));
    expect(find.text('Aprovado'), findsNothing);
    expect(find.text('Aprovar'), findsNWidgets(5));

    await tocar(tester, find.byTooltip('Recusar ajuste de Juliana Prado'));
    // Recusar pede o motivo (mínimo de 5 caracteres) antes de gravar.
    expect(find.text('Recusar ajuste de ponto'), findsOneWidget);
    await digitar(tester, 'Motivo da recusa', 'Horário já conferido no sistema.');
    await tocar(tester, find.text('Recusar pedido'));
    expect(find.text('Recusado'), findsOneWidget);
  });

  testWidgets('funcionários: busca pelo código FUNC e CPF mascarado com consulta registrada', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'funcionarios');

    expect(find.text('Mostrando 1–20 de 248'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('busca_funcionarios')), '142');
    await tester.pumpAndSettle();
    expect(find.text('Mariana Costa'), findsOneWidget);
    expect(find.text('Mostrando 1–1 de 1'), findsOneWidget);

    final cpfMascarado = find.textContaining(RegExp(r'^\*\*\*\.\d{3}\.\d{3}-\*\*$'));
    final cpfCompleto = find.textContaining(RegExp(r'^\d{3}\.\d{3}\.\d{3}-\d{2}$'));
    expect(cpfMascarado, findsOneWidget);
    expect(cpfCompleto, findsNothing);

    await tocar(tester, find.byTooltip('Mostrar CPF de Mariana Costa'));
    expect(cpfCompleto, findsOneWidget);
    expect(find.text('Consulta ao CPF de Mariana Costa registrada (LGPD).'), findsOneWidget);

    // RF05: a busca não é por nome.
    await tester.enterText(find.byKey(const Key('busca_funcionarios')), 'Mariana');
    await tester.pumpAndSettle();
    expect(find.textContaining('A busca é feita pelo código FUNC'), findsOneWidget);
    expect(find.text('Nenhum funcionário encontrado com estes filtros.'), findsOneWidget);

    // A consulta aparece no registro da LGPD em Configurações.
    await abrirAba(tester, 'configuracoes');
    expect(find.textContaining('CPF de Mariana Costa (FUNC-0142) visto por FUNC-0001'), findsOneWidget);
  });

  testWidgets('novo funcionário: valida, gera o código FUNC e aparece na lista', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'novoFuncionario');

    expect(find.text('FUNC-0253'), findsOneWidget);

    await tocar(tester, find.text('Cadastrar funcionário'));
    expect(find.text('Revise os campos destacados.'), findsOneWidget);
    expect(find.text('Informe o CPF.'), findsOneWidget);

    await digitar(tester, 'Nome completo', 'Ana Beatriz Souza');
    await digitar(tester, 'CPF', '52998224725');
    await digitar(tester, 'RG', '12.345.678-9');
    await digitar(tester, 'Data de nascimento', '15031995');
    await digitar(tester, 'E-mail corporativo', 'ana.beatriz@hrauto.local');
    await digitar(tester, 'Telefone', '13991234567');
    await escolher(tester, 'Cargo', 'Desenvolvedor');
    await escolher(tester, 'Setor', 'Tecnologia');
    await digitar(tester, 'Data de admissão', '03112026');
    await digitar(tester, 'CEP', '11015000');
    await digitar(tester, 'Logradouro', 'Rua Azevedo Sodré');
    await digitar(tester, 'Número', '120');
    await digitar(tester, 'Bairro', 'Gonzaga');
    await digitar(tester, 'Cidade', 'Santos');
    await digitar(tester, 'UF', 'SP');
    await tester.pumpAndSettle();

    expect(find.text('529.982.247-25'), findsOneWidget); // máscara de CPF
    expect(find.text('Gestor: Bruno Carvalho'), findsOneWidget);

    await tocar(tester, find.text('Cadastrar funcionário'));

    expect(find.textContaining('FUNC-0253 cadastrado. O código é definitivo'), findsOneWidget);
    expect(find.text('Ana Beatriz Souza'), findsOneWidget);
    expect(find.text('Novo'), findsOneWidget);
    expect(find.text('Mostrando 1–20 de 249'), findsOneWidget);
  });

  testWidgets('registro de ponto: batida em um toque segue a sequência e o saldo aparece', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'ponto');

    expect(find.text('+12h56'), findsOneWidget); // 12h30 anteriores + 9 min (seg) + 17 min (ter)
    expect(find.text('Registrar entrada'), findsOneWidget);

    await tocar(tester, find.byKey(const Key('botao_bater_ponto')));
    expect(find.text('Entrada registrada às 10:30.'), findsOneWidget);
    expect(find.text('Registrar saída para intervalo'), findsOneWidget);

    await tocar(tester, find.text('Solicitar ajuste'));
    await digitar(tester, 'Horário correto', '0800');
    await digitar(tester, 'Justificativa', 'Cheguei às 8h, mas o app travou na portaria.');
    await tocar(tester, find.text('Enviar pedido'));
    expect(find.text('Pedido de ajuste enviado ao seu gestor.'), findsOneWidget);
    expect(find.text('Aguardando gestor'), findsOneWidget);
  });

  testWidgets('advertências: a 3ª leve gera suspensão para o RH aprovar', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'advertencias');

    expect(find.text('1 leve para suspensão'), findsNWidgets(2)); // Lucas Ferreira e Thiago Ramos

    await tocar(tester, find.text('Registrar advertência'));
    await digitar(tester, 'Código do funcionário', '64');
    await escolher(tester, 'Motivo', 'Atraso recorrente (leve)');
    await digitar(tester, 'Descrição', 'Atraso de 35 min.');
    await tocar(tester, find.text('Registrar'));

    expect(find.text('Régua de consequências: suspensão de Lucas Ferreira aguardando aprovação do RH.'), findsOneWidget);
    expect(find.text('Suspensões aguardando aprovação'), findsOneWidget);
    expect(find.text('Suspensão aguardando aprovação do RH'), findsOneWidget);

    await tocar(tester, find.text('Aprovar suspensão'));
    expect(find.text('Suspensão de Lucas Ferreira aprovada.'), findsOneWidget);
    expect(find.text('Suspensões aguardando aprovação'), findsNothing);
  });

  testWidgets('código inexistente mostra o erro no diálogo de advertência', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'advertencias');

    await tocar(tester, find.text('Registrar advertência'));
    await digitar(tester, 'Código do funcionário', '9999');
    await escolher(tester, 'Motivo', 'Insubordinação (grave)');
    await digitar(tester, 'Descrição', 'Teste.');
    await tocar(tester, find.text('Registrar'));

    expect(find.text('Nenhum funcionário com o código FUNC-9999.'), findsOneWidget);
  });

  testWidgets('compliance: publicar aviso entra nas leituras obrigatórias', (tester) async {
    await entrarComoAdmin(tester);
    await abrirAba(tester, 'compliance');

    expect(find.text('Leituras obrigatórias'), findsOneWidget);
    expect(find.text('Vitrine de benefícios'), findsOneWidget);
    expect(find.text('Teste de perfil (DISC)'), findsOneWidget);

    await tocar(tester, find.text('Publicar aviso'));
    await digitar(tester, 'Título do aviso', 'Campanha de vacinação');
    await digitar(tester, 'Expira em', '31122030');
    await tocar(tester, find.text('Publicar'));

    expect(find.text('"Campanha de vacinação" publicado para 248 colaboradores.'), findsOneWidget);
    expect(find.text('Campanha de vacinação'), findsOneWidget);
    expect(find.text('0/248 leram'), findsOneWidget);
  });

  testWidgets('celular: mesmo menu de ícones, uma aba por vez', (tester) async {
    await entrarComoAdmin(tester, tamanho: const Size(390, 844));

    expect(find.byTooltip('Configurações'), findsOneWidget);
    await abrirAba(tester, 'configuracoes');

    expect(find.text('Níveis de acesso'), findsOneWidget);
    expect(find.text('Canal de conversa com o RH'), findsOneWidget);
    expect(find.text('Ajustes de ponto pendentes'), findsNothing);
  });
}
