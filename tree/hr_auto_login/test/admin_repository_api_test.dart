import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/data/repositories/admin_repository_api.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/ponto_repository_api.dart';
import 'package:hr_auto/domain/entities/advertencia.dart';
import 'package:hr_auto/domain/entities/estrutura_empresa.dart';
import 'package:hr_auto/domain/entities/funcionario.dart';
import 'package:hr_auto/domain/entities/ponto.dart';

/// AdminRepositoryApi (Fase 5) contra um MockClient que responde como a API v1 da Fase 4.
void main() {
  const token = 'jwt-de-teste';

  http.Response ok(Object? corpo, [int status = 200]) => http.Response(
        jsonEncode(corpo),
        status,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );

  /// Roteador: chave "METODO /caminho". Rota desconhecida = 404. Guarda as chamadas em [log].
  MockClient roteador(Map<String, http.Response Function(http.Request)> rotas, List<http.Request> log) =>
      MockClient((req) async {
        log.add(req);
        final rota = rotas['${req.method} ${req.url.path}'];
        return rota == null ? http.Response('', 404) : rota(req);
      });

  AdminRepositoryApi criar(MockClient cliente) {
    final api = ApiClient(cliente: cliente, baseUrl: 'http://api');
    return AdminRepositoryApi(
      api,
      token: () => token,
      ponto: PontoRepositoryApi(api, token: () => token),
      prototipo: AdminRepositoryPrototipo(latencia: Duration.zero, relogio: () => DateTime(2026, 10, 25, 9)),
    );
  }

  Map<String, dynamic> func(int id, {String nome = 'Ana Souza', String depto = 'Operações', bool ativo = true}) => {
        'codigo': Funcionario.codigoDe(id),
        'nome': nome,
        'email': '${nome.split(' ').first.toLowerCase()}@hrauto.local',
        'cargo': 'Analista',
        'departamento': depto,
        'codigoGestor': null,
        'nomeGestor': null,
        'ativo': ativo,
        'dataAdmissao': '2025-03-10',
      };

  Map<String, dynamic> pagina(List<Map<String, dynamic>> itens, {int? total, int numero = 1}) =>
      {'itens': itens, 'pagina': numero, 'tamanho': 100, 'total': total ?? itens.length};

  Map<String, dynamic> adv(int id, int codigo, String nome, {String tipo = 'Leve', String status = 'Ativa'}) => {
        'id': id,
        'codigoFuncionario': Funcionario.codigoDe(codigo),
        'nomeFuncionario': nome,
        'tipo': tipo,
        'data': '2026-10-0$id',
        'motivo': 'Atraso recorrente',
        'descricao': 'Chegou atrasado.',
        'status': status,
        'codigoResponsavel': 'FUNC-0002',
        'nomeResponsavel': 'Renata Lopes',
      };

  Map<String, dynamic> pedido({int id = 41, String justificativa = '[saida 19:12] Esqueci de registrar a saída.'}) => {
        'id': id,
        'codigoFuncionario': 'FUNC-0003',
        'nomeFuncionario': 'Bruno Lima',
        'dataPonto': '2026-10-17',
        'batidas': <Object>[],
        'justificativa': justificativa,
        'dataSolicitacao': '2026-10-20T18:40:00',
        'status': 'Pendente',
        'dataAnalise': null,
        'codigoResponsavel': null,
        'observacao': null,
      };

  group('Funcionários', () {
    test('lê todas as páginas e mostra os mais recentes primeiro', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/funcionarios': (req) {
          final pag = int.parse(req.url.queryParameters['pagina']!);
          return ok(pagina(pag == 1 ? [func(1), func(2)] : [func(3, nome: 'Caio Dias')], total: 3, numero: pag));
        },
      }, log));

      final lista = await repo.listarFuncionarios();

      expect(lista.map((f) => f.codigo), ['FUNC-0003', 'FUNC-0002', 'FUNC-0001']);
      expect(lista.first.setor, 'Operações');
      expect(lista.first.cpf, isEmpty); // CPF só vem na ficha (LGPD)
      expect(log.map((r) => r.url.queryParameters['pagina']), ['1', '2']);
      expect(log.every((r) => r.headers['Authorization'] == 'Bearer $token'), isTrue);
    });

    test('cadastro troca nomes de cargo e setor pelos ids da API e envia o perfil', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/cargos': (_) => ok([
              {'id': 1, 'nome': 'Analista de RH', 'descricao': null},
              {'id': 2, 'nome': 'Desenvolvedor', 'descricao': null},
            ]),
        'GET /api/v1/departamentos': (_) => ok([
              {'id': 3, 'nome': 'Tecnologia', 'descricao': 'Sistemas'},
            ]),
        'POST /api/v1/funcionarios': (_) => ok({
              'codigo': 'FUNC-0253',
              'nome': 'Diego Alves',
              'acessoCriado': true,
              'perfil': 'RH',
              'conviteEnviado': true,
            }, 201),
      }, log));

      final criado = await repo.cadastrarFuncionario(NovoFuncionario(
        nome: 'Diego Alves',
        cpf: '52998224725',
        rg: '12.345.678-9',
        dataNascimento: DateTime(1999, 3, 4),
        dataAdmissao: DateTime(2026, 10, 26),
        email: 'diego@hrauto.local',
        telefone: '(13) 99999-0000',
        cargo: const Cargo(nome: 'Desenvolvedor', cargaHorariaSemanal: 40),
        setor: const Setor(nome: 'Tecnologia'),
        nivel: NivelAcesso.rh,
        endereco: const Endereco(
          cep: '11010000',
          logradouro: 'Rua das Flores',
          numero: '100',
          bairro: 'Centro',
          cidade: 'Santos',
          uf: 'SP',
        ),
        enviarConvite: true,
      ));

      final corpo = jsonDecode(log.firstWhere((r) => r.method == 'POST').body) as Map<String, dynamic>;
      expect(corpo['idCargo'], 2);
      expect(corpo['idDepartamento'], 3);
      expect(corpo['perfil'], 'RH');
      expect(corpo['criarAcesso'], isTrue);
      expect(corpo['telefone'], '13999990000');
      expect(corpo['dataNascimento'], '1999-03-04');
      expect(corpo['dataAdmissao'], '2026-10-26');
      expect((corpo['endereco'] as Map).containsKey('complemento'), isFalse);
      expect(criado.codigo, 'FUNC-0253');
      expect(criado.id, 253);
      expect(criado.nivel, NivelAcesso.rh);
    });

    test('cargo que não existe na API é recusado sem chamar o cadastro', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/cargos': (_) => ok([
              {'id': 1, 'nome': 'Analista de RH', 'descricao': null},
            ]),
        'GET /api/v1/departamentos': (_) => ok([
              {'id': 3, 'nome': 'Tecnologia', 'descricao': null},
            ]),
      }, log));

      await expectLater(
        repo.cadastrarFuncionario(NovoFuncionario(
          nome: 'Diego Alves',
          cpf: '52998224725',
          rg: '123456789',
          dataNascimento: DateTime(1999, 3, 4),
          dataAdmissao: DateTime(2026, 10, 26),
          email: 'diego@hrauto.local',
          telefone: '13999990000',
          cargo: const Cargo(nome: 'Astronauta', cargaHorariaSemanal: 40),
          setor: const Setor(nome: 'Tecnologia'),
          nivel: NivelAcesso.colaborador,
          endereco: const Endereco(cep: '11010000', logradouro: 'Rua A', numero: '1', bairro: 'B', cidade: 'Santos', uf: 'SP'),
          enviarConvite: false,
        )),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('não existe na API'))),
      );
      expect(log.any((r) => r.method == 'POST'), isFalse);
    });

    test('desligar e reativar chamam as rotas pelo código FUNC', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'POST /api/v1/funcionarios/FUNC-0087/desligamento': (_) => http.Response('', 204),
        'POST /api/v1/funcionarios/FUNC-0087/reativacao': (_) => http.Response('', 204),
      }, log));

      await repo.alterarSituacaoFuncionario(id: 87, ativo: false);
      await repo.alterarSituacaoFuncionario(id: 87, ativo: true);

      expect(log.map((r) => r.url.path), [
        '/api/v1/funcionarios/FUNC-0087/desligamento',
        '/api/v1/funcionarios/FUNC-0087/reativacao',
      ]);
    });

    test('registrar consulta de dados pessoais lê a ficha (a API grava a auditoria)', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/funcionarios/FUNC-0005': (_) => ok({
              ...func(5, nome: 'Bruno Lima'),
              'telefone': '13999990000',
              'dataNascimento': '1990-02-03',
              'documentos': {'cpf': '52998224725', 'rg': '123456789'},
              'endereco': {'cep': '11010000', 'logradouro': 'Rua A', 'numero': '10', 'bairro': 'Centro', 'cidade': 'Santos', 'uf': 'SP'},
            }),
      }, log));

      await repo.registrarConsultaDadosPessoais(funcionarioId: 5, consultadoPor: 'FUNC-0001 · Administrador');
      final ficha = await repo.obterFicha(5);

      expect(log.length, 2);
      expect(ficha.cpf, '52998224725');
      expect(ficha.dataNascimento, DateTime(1990, 2, 3));
      expect(ficha.endereco.cidade, 'Santos');
      expect(await repo.listarConsultasDadosPessoais(), isEmpty);
    });
  });

  group('Advertências', () {
    test('registra como Leve e devolve a sugestão da régua quando ela pede suspensão', () async {
      late Map<String, dynamic> corpo;
      final repo = criar(roteador({
        'POST /api/v1/advertencias': (req) {
          corpo = jsonDecode(req.body) as Map<String, dynamic>;
          return ok({
            'advertencia': adv(3, 95, 'Roberto Dias'),
            'semaforo': {'nivel': 'Vermelho', 'quantidadeAtivas': 3},
            'regua': {
              'acao': 'Suspensao',
              'levesDesdeUltimaSuspensao': 3,
              'suspensoesAtivas': 0,
              'mensagem': 'Três advertências leves: sugerida 1 suspensão.',
            },
          }, 201);
        },
      }, []));

      final sugestao = await repo.registrarAdvertencia(
        codigoFuncionario: 'func-0095',
        motivo: MotivoAdvertencia.padrao.first,
        descricao: 'Chegou atrasado.',
        registradaPor: 'Renata Lopes',
      );

      expect(corpo['codigoFuncionario'], 'FUNC-0095');
      expect(corpo['tipo'], 'Leve');
      expect(corpo['motivo'], 'Atraso recorrente');
      expect(sugestao, isNotNull);
      expect(sugestao!.medida, Medida.suspensao);
      expect(sugestao.aguardandoAprovacao, isTrue);
      expect(sugestao.descricao, contains('sugerida 1 suspensão'));
    });

    test('sem consequência da régua devolve null', () async {
      final repo = criar(roteador({
        'POST /api/v1/advertencias': (_) => ok({
              'advertencia': adv(1, 95, 'Roberto Dias'),
              'semaforo': {'nivel': 'Amarelo', 'quantidadeAtivas': 1},
              'regua': {'acao': 'Nenhuma', 'levesDesdeUltimaSuspensao': 1, 'suspensoesAtivas': 0, 'mensagem': ''},
            }, 201),
      }, []));

      final sugestao = await repo.registrarAdvertencia(
        codigoFuncionario: 'FUNC-0095',
        motivo: MotivoAdvertencia.padrao.first,
        descricao: '',
        registradaPor: 'Renata Lopes',
      );

      expect(sugestao, isNull);
    });

    test('lista mapeia tipo, status e setor', () async {
      final repo = criar(roteador({
        'GET /api/v1/funcionarios': (_) => ok(pagina([func(95, nome: 'Roberto Dias')])),
        'GET /api/v1/advertencias': (_) => ok(pagina([
              adv(1, 95, 'Roberto Dias'),
              adv(2, 95, 'Roberto Dias', tipo: 'Suspensao'),
              adv(3, 95, 'Roberto Dias', status: 'Cancelada'),
            ])),
      }, []));

      final lista = await repo.listarAdvertencias();

      expect(lista.map((a) => a.id), ['1', '2', '3']);
      expect(lista[0].medida, Medida.advertencia);
      expect(lista[1].medida, Medida.suspensao);
      expect(lista[2].ativa, isFalse);
      expect(lista[0].colaborador.departamento, 'Operações');
      expect(lista[0].motivo.gravidade, Gravidade.leve);
      expect(lista[0].registradaPor, 'Renata Lopes');
    });

    test('aprovar suspensão não existe na API e avisa', () async {
      final repo = criar(roteador({}, []));

      await expectLater(repo.aprovarSuspensao('300'), throwsA(isA<Falha>()));
    });
  });

  group('Ajustes de ponto (decisão do RH)', () {
    test('aprovar busca o pedido pendente e envia a batida pedida em HH:mm:ss', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/manutencoes-ponto': (_) => ok(pagina([pedido()])),
        'POST /api/v1/manutencoes-ponto/41/decisao': (_) => ok({
              'manutencao': {...pedido(), 'status': 'Aprovada'},
              'batidasAlteradas': true,
              'alteracoes': <Object>[],
              'fechamentoDoDia': null,
            }),
      }, log));

      await repo.decidirAjuste(id: '41', situacao: SituacaoAjuste.aprovado);

      final consulta = log.first.url.queryParameters;
      expect(consulta['escopo'], 'todas');
      expect(consulta['status'], 'Pendente');
      final corpo = jsonDecode(log.last.body) as Map<String, dynamic>;
      expect(corpo['aprovar'], isTrue);
      expect(corpo['batidas'], {'saida': '19:12:00'});
    });

    test('recusar sem motivo usa o texto padrão (a API exige 5+ caracteres)', () async {
      late Map<String, dynamic> corpo;
      final repo = criar(roteador({
        'POST /api/v1/manutencoes-ponto/41/decisao': (req) {
          corpo = jsonDecode(req.body) as Map<String, dynamic>;
          return ok({'manutencao': pedido(), 'batidasAlteradas': false, 'alteracoes': <Object>[]});
        },
      }, []));

      await repo.decidirAjuste(id: '41', situacao: SituacaoAjuste.recusado, observacao: '  ');

      expect(corpo['aprovar'], isFalse);
      expect((corpo['observacao'] as String).length, greaterThanOrEqualTo(5));
      expect(corpo.containsKey('batidas'), isFalse);
    });

    test('desfazer decisão e pedido inexistente são recusados', () async {
      final repo = criar(roteador({
        'GET /api/v1/manutencoes-ponto': (_) => ok(pagina([])),
      }, []));

      await expectLater(repo.decidirAjuste(id: '41', situacao: SituacaoAjuste.pendente), throwsA(isA<Falha>()));
      await expectLater(repo.decidirAjuste(id: '99', situacao: SituacaoAjuste.aprovado), throwsA(isA<Falha>()));
      await expectLater(repo.decidirAjuste(id: 'aj-0412', situacao: SituacaoAjuste.aprovado), throwsA(isA<Falha>()));
    });
  });

  group('Dashboard híbrido', () {
    test('quadro, semáforo e ajustes pendentes vêm da API; indicadores sem endpoint, do exemplo', () async {
      final log = <http.Request>[];
      final repo = criar(roteador({
        'GET /api/v1/funcionarios': (_) =>
            ok(pagina([func(1), func(2, nome: 'Bruno Lima'), func(3, nome: 'Caio Dias', depto: 'Comercial')])),
        'GET /api/v1/manutencoes-ponto': (_) => ok(pagina([pedido()])),
        'GET /api/v1/advertencias': (_) => ok(pagina([
              adv(1, 1, 'Ana Souza'),
              adv(2, 2, 'Bruno Lima'),
              adv(3, 2, 'Bruno Lima'),
              adv(4, 2, 'Bruno Lima'),
            ])),
      }, log));

      final d = await repo.carregarDashboard();

      expect(d.resumo.colaboradoresAtivos, 3);
      expect(d.ajustes.single.id, '41');
      expect(d.ajustes.single.tipoBatida, TipoBatida.saida);
      expect(d.ajustesPendentes, 1);
      expect(d.semaforo.amarelo, 1); // FUNC-0001 com 1 advertência
      expect(d.semaforo.vermelho, 1); // FUNC-0002 com 3
      expect(d.semaforo.verde, 1);
      expect(d.jornadaPorSetor, isNotEmpty); // exemplo (sem endpoint)
      expect(log.where((r) => r.url.queryParameters['ativo'] == 'true'), isNotEmpty);
    });

    test('compliance continua com dados de exemplo', () async {
      final repo = criar(roteador({}, []));

      final central = await repo.carregarCompliance();

      expect(central.leituras, isNotEmpty);
    });
  });

  group('Estrutura da empresa', () {
    test('carrega cargos e departamentos e cria cargo só com nome e descrição', () async {
      late Map<String, dynamic> corpo;
      final repo = criar(roteador({
        'GET /api/v1/cargos': (_) => ok([
              {'id': 1, 'nome': 'Analista de RH', 'descricao': 'Pessoas'},
            ]),
        'GET /api/v1/departamentos': (_) => ok([
              {'id': 3, 'nome': 'Tecnologia', 'descricao': null},
            ]),
        'POST /api/v1/cargos': (req) {
          corpo = jsonDecode(req.body) as Map<String, dynamic>;
          return ok({'id': 9, 'nome': 'Designer', 'descricao': null}, 201);
        },
      }, []));

      final estrutura = await repo.carregarEstrutura();
      await repo.adicionarCargo(const Cargo(nome: 'Designer', cargaHorariaSemanal: 30));

      expect(estrutura.cargos.single.nome, 'Analista de RH');
      expect(estrutura.cargos.single.cargaHorariaSemanal, AdminRepositoryApi.cargaSemanalPadrao);
      expect(estrutura.setores.single.nome, 'Tecnologia');
      expect(corpo, {'nome': 'Designer'});
    });
  });
}
