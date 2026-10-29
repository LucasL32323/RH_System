import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/formatting/formatadores.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/data/repositories/admin_repository_api.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/ponto_repository_api.dart';
import 'package:hr_auto/domain/entities/ponto.dart';
import 'package:hr_auto/domain/repositories/admin_repository.dart';
import 'package:hr_auto/presentation/providers.dart';
import 'package:hr_auto/presentation/viewmodels/dashboard_admin_view_model.dart';
import 'package:hr_auto/presentation/viewmodels/funcionarios_view_model.dart';
import 'package:hr_auto/presentation/widgets/admin/dialogo_ficha_funcionario.dart';
import 'package:hr_auto/presentation/widgets/admin/dialogo_motivo_recusa.dart';

/// Parte 3 da Fase 5: ficha completa, motivo da recusa e observação nos pedidos de ajuste.
/// Repositórios em memória ou MockClient; sem rede.

/// Protótipo que registra a observação recebida e, se pedido, falha como a API faria.
class _EspiaoAdmin extends AdminRepositoryPrototipo {
  _EspiaoAdmin({this.falhar = false}) : super(latencia: Duration.zero, relogio: () => DateTime(2026, 10, 26, 9));

  final bool falhar;
  String? observacaoRecebida;

  @override
  Future<void> decidirAjuste({required String id, required SituacaoAjuste situacao, String? observacao}) async {
    observacaoRecebida = observacao;
    if (falhar) throw const Falha('Este pedido já foi decidido.');
    return super.decidirAjuste(id: id, situacao: situacao, observacao: observacao);
  }
}

ProviderContainer _container(AdminRepository repo) {
  final container = ProviderContainer(overrides: [adminRepositoryProvider.overrideWithValue(repo)]);
  addTearDown(container.dispose);
  return container;
}

void main() {
  http.Response resposta(Object corpo) => http.Response(
        jsonEncode(corpo),
        200,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );

  Map<String, dynamic> manutencao({String status = 'Pendente', Object? observacao}) => {
        'id': 7,
        'codigoFuncionario': 'FUNC-0001',
        'nomeFuncionario': 'Administrador do Sistema',
        'dataPonto': '2026-10-20',
        'batidas': <Object>[],
        'justificativa': '[entrada 08:02] O app não abriu na portaria.',
        'dataSolicitacao': '2026-10-25T08:30:00',
        'status': status,
        'dataAnalise': null,
        'codigoResponsavel': null,
        'observacao': observacao,
      };

  group('Observação no pedido de ajuste', () {
    test('a observação do gestor vem do JSON; vazia ou ausente vira null', () {
      final recusado = PontoRepositoryApi.ajusteDeJson(
        manutencao(status: 'Rejeitada', observacao: '  Horário já conferido no sistema.  '),
      );
      final embranco = PontoRepositoryApi.ajusteDeJson(manutencao(status: 'Aprovada', observacao: '   '));
      final ausente = PontoRepositoryApi.ajusteDeJson(manutencao());

      expect(recusado.situacao, SituacaoAjuste.recusado);
      expect(recusado.observacao, 'Horário já conferido no sistema.');
      expect(embranco.observacao, isNull);
      expect(ausente.observacao, isNull);
    });

    test('voltar para pendente apaga a observação; decidir sem texto novo mantém a anterior', () {
      final base = PontoRepositoryApi.ajusteDeJson(manutencao(status: 'Rejeitada', observacao: 'Fora do prazo.'));

      expect(base.copyWith(situacao: SituacaoAjuste.pendente).observacao, isNull);
      expect(base.copyWith(situacao: SituacaoAjuste.aprovado).observacao, 'Fora do prazo.');
      expect(base.copyWith(observacao: 'Outro motivo.').observacao, 'Outro motivo.');
    });
  });

  group('Ficha completa (ViewModel)', () {
    test('protótipo: devolve a ficha e grava a consulta no log da LGPD', () async {
      final container = _container(AdminRepositoryPrototipo(latencia: Duration.zero, relogio: () => DateTime(2026, 10, 26, 9)));
      final lista = await container.read(funcionariosViewModelProvider.future);
      final alvo = lista.firstWhere((f) => f.id == 142);

      final ficha = await container
          .read(funcionariosViewModelProvider.notifier)
          .consultarFicha(alvo, consultadoPor: 'FUNC-0001 · Administrador do Sistema');

      expect(ficha.cpf, alvo.cpf);
      expect(ficha.rg, alvo.rg);
      final consultas = await container.read(consultasDadosPessoaisProvider.future);
      expect(consultas.single.funcionario.matricula, 'FUNC-0142');
      expect(consultas.single.consultadoPor, 'FUNC-0001 · Administrador do Sistema');
    });

    test('API: a ficha substitui a linha da lista e a situação Ativo/Inativo da lista é mantida', () async {
      final log = <http.Request>[];
      final cliente = MockClient((req) async {
        log.add(req);
        final resumo = {
          'codigo': 'FUNC-0005',
          'nome': 'Bruno Lima',
          'email': 'bruno@hrauto.local',
          'cargo': 'Analista',
          'departamento': 'Operações',
          'codigoGestor': null,
          'nomeGestor': null,
          'ativo': false,
          'dataAdmissao': '2025-03-10',
        };
        if (req.url.path == '/api/v1/funcionarios') {
          return resposta({'itens': [resumo], 'pagina': 1, 'tamanho': 100, 'total': 1});
        }
        if (req.url.path == '/api/v1/funcionarios/FUNC-0005') {
          // A ficha não traz "ativo": o app não pode reativar a pessoa só por consultar.
          return resposta({
            ...(Map<String, dynamic>.of(resumo)..remove('ativo')),
            'telefone': '13999990000',
            'dataNascimento': '1990-02-03',
            'documentos': {'cpf': '52998224725', 'rg': '123456789'},
            'endereco': {'cep': '11010000', 'logradouro': 'Rua A', 'numero': '10', 'bairro': 'Centro', 'cidade': 'Santos', 'uf': 'SP'},
          });
        }
        return http.Response('', 404);
      });
      final api = ApiClient(cliente: cliente, baseUrl: 'http://api');
      final repo = AdminRepositoryApi(
        api,
        token: () => 'jwt-de-teste',
        ponto: PontoRepositoryApi(api, token: () => 'jwt-de-teste'),
        prototipo: AdminRepositoryPrototipo(latencia: Duration.zero),
      );
      final container = _container(repo);

      final lista = await container.read(funcionariosViewModelProvider.future);
      expect(lista.single.cpf, isEmpty); // a lista não traz CPF
      expect(lista.single.ativo, isFalse);

      final ficha = await container
          .read(funcionariosViewModelProvider.notifier)
          .consultarFicha(lista.single, consultadoPor: 'FUNC-0001 · Administrador do Sistema');

      expect(ficha.cpf, '52998224725');
      final atualizada = container.read(funcionariosViewModelProvider).valueOrNull!.single;
      expect(atualizada.cpf, '52998224725');
      expect(atualizada.rg, '123456789');
      expect(atualizada.dataNascimento, DateTime(1990, 2, 3));
      expect(atualizada.ativo, isFalse);
      expect(log.where((r) => r.url.path == '/api/v1/funcionarios/FUNC-0005').length, 1);
    });
  });

  group('Decisão de ajuste (ViewModel)', () {
    test('recusar com motivo envia a observação ao repositório e guarda no pedido', () async {
      final espiao = _EspiaoAdmin();
      final container = _container(espiao);
      await container.read(dashboardAdminViewModelProvider.future);
      final vm = container.read(dashboardAdminViewModelProvider.notifier);

      final ok = await vm.decidir('aj-0412', SituacaoAjuste.recusado, observacao: 'Horário já conferido.');

      expect(ok, isTrue);
      expect(vm.ultimoErro, isNull);
      expect(espiao.observacaoRecebida, 'Horário já conferido.');
      final ajuste = container.read(dashboardAdminViewModelProvider).valueOrNull!.ajustes.firstWhere((a) => a.id == 'aj-0412');
      expect(ajuste.situacao, SituacaoAjuste.recusado);
      expect(ajuste.observacao, 'Horário já conferido.');

      // Desfazer (só o protótipo permite) volta para pendente e apaga o motivo.
      expect(await vm.decidir('aj-0412', SituacaoAjuste.pendente), isTrue);
      final desfeito = container.read(dashboardAdminViewModelProvider).valueOrNull!.ajustes.firstWhere((a) => a.id == 'aj-0412');
      expect(desfeito.situacao, SituacaoAjuste.pendente);
      expect(desfeito.observacao, isNull);
    });

    test('falha do repositório volta o pedido para pendente e guarda a mensagem', () async {
      final container = _container(_EspiaoAdmin(falhar: true));
      await container.read(dashboardAdminViewModelProvider.future);
      final vm = container.read(dashboardAdminViewModelProvider.notifier);

      final ok = await vm.decidir('aj-0412', SituacaoAjuste.recusado, observacao: 'Fora do prazo.');

      expect(ok, isFalse);
      expect(vm.ultimoErro, 'Este pedido já foi decidido.');
      final ajuste = container.read(dashboardAdminViewModelProvider).valueOrNull!.ajustes.firstWhere((a) => a.id == 'aj-0412');
      expect(ajuste.situacao, SituacaoAjuste.pendente);
      expect(ajuste.observacao, isNull);
    });
  });

  group('Diálogos', () {
    testWidgets('motivo da recusa: valida o mínimo, devolve o texto e cancelar devolve null', (tester) async {
      String? resultado = 'nenhum';
      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: TextButton(
                onPressed: () async => resultado = await pedirMotivoRecusa(context, colaborador: 'Rafael Nogueira'),
                child: const Text('Abrir'),
              ),
            ),
          ),
        ),
      ));

      await tester.tap(find.text('Abrir'));
      await tester.pumpAndSettle();
      expect(find.text('Recusar ajuste de ponto'), findsOneWidget);

      // Vazio e curto demais: o diálogo continua aberto.
      await tester.tap(find.text('Recusar pedido'));
      await tester.pumpAndSettle();
      expect(find.text('Informe o motivo da recusa.'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('campo_Motivo da recusa')), 'abc');
      await tester.tap(find.text('Recusar pedido'));
      await tester.pumpAndSettle();
      expect(find.text('Use pelo menos 5 caracteres.'), findsOneWidget);
      expect(resultado, 'nenhum');

      await tester.enterText(find.byKey(const Key('campo_Motivo da recusa')), '  Horário já conferido no sistema.  ');
      await tester.tap(find.text('Recusar pedido'));
      await tester.pumpAndSettle();
      expect(resultado, 'Horário já conferido no sistema.');
      expect(find.text('Recusar ajuste de ponto'), findsNothing);

      // Cancelar devolve null.
      await tester.tap(find.text('Abrir'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(resultado, isNull);
    });

    testWidgets('ficha do funcionário mostra CPF completo, RG e telefone formatado e fecha', (tester) async {
      final lista = await AdminRepositoryPrototipo(latencia: Duration.zero).listarFuncionarios();
      final f = lista.firstWhere((x) => x.id == 142);

      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: TextButton(
                onPressed: () => showDialog<void>(context: context, builder: (_) => DialogoFichaFuncionario(funcionario: f)),
                child: const Text('Abrir'),
              ),
            ),
          ),
        ),
      ));

      await tester.tap(find.text('Abrir'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('dialogo_ficha')), findsOneWidget);
      expect(find.text(f.nome), findsOneWidget);
      expect(find.text(Formatadores.cpf(f.cpf)), findsOneWidget);
      expect(find.text(f.rg), findsOneWidget);
      expect(find.text(DialogoFichaFuncionario.telefoneFormatado(f.telefone)), findsOneWidget);
      expect(find.textContaining('log de privacidade'), findsOneWidget);

      await tester.tap(find.text('Fechar'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('dialogo_ficha')), findsNothing);
    });

    test('formatação do telefone e do endereço da ficha', () {
      expect(DialogoFichaFuncionario.telefoneFormatado('13991234567'), '(13) 99123-4567');
      expect(DialogoFichaFuncionario.telefoneFormatado('1332211234'), '(13) 3221-1234');
      expect(DialogoFichaFuncionario.telefoneFormatado(''), '—');
    });
  });
}
