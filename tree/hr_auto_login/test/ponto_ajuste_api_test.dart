import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/data/repositories/ponto_repository_api.dart';
import 'package:hr_auto/domain/entities/ponto.dart';

/// Pedido de ajuste de ponto (Fase 5): o MockClient responde como POST/GET /api/v1/manutencoes-ponto.
void main() {
  const token = 'jwt-de-teste';

  http.Response resposta(Object corpo, [int status = 200]) => http.Response(
        jsonEncode(corpo),
        status,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );

  Map<String, dynamic> manutencao({
    int id = 7,
    String justificativa = '[entrada 08:02] O app não abriu na portaria.',
    String status = 'Pendente',
    List<Map<String, dynamic>> batidas = const [
      {'tipo': 'Entrada', 'nome': 'entrada', 'horario': '08:47:10'},
    ],
  }) =>
      {
        'id': id,
        'codigoFuncionario': 'FUNC-0001',
        'nomeFuncionario': 'Administrador do Sistema',
        'dataPonto': '2026-10-20',
        'batidas': batidas,
        'justificativa': justificativa,
        'dataSolicitacao': '2026-10-25T08:30:00.1234567',
        'status': status,
        'dataAnalise': null,
        'codigoResponsavel': null,
        'observacao': null,
      };

  PontoRepositoryApi repo(MockClient cliente) =>
      PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => token);

  group('solicitarAjuste', () {
    test('envia a data e a justificativa com tipo e horário no prefixo, e devolve o pedido pendente', () async {
      late http.Request enviado;
      final r = repo(MockClient((req) async {
        enviado = req;
        return resposta(manutencao(), 201);
      }));

      final ajuste = await r.solicitarAjuste(
        data: DateTime(2026, 10, 20),
        tipoBatida: TipoBatida.entrada,
        horarioSolicitado: '08:02',
        justificativa: '  O app não abriu na portaria.  ',
      );

      expect(enviado.method, 'POST');
      expect(enviado.url.path, '/api/v1/manutencoes-ponto');
      expect(enviado.headers['Authorization'], 'Bearer $token');
      expect(jsonDecode(enviado.body), {
        'data': '2026-10-20',
        'justificativa': '[entrada 08:02] O app não abriu na portaria.',
      });

      expect(ajuste.id, '7');
      expect(ajuste.tipoBatida, TipoBatida.entrada);
      expect(ajuste.horarioSolicitado, '08:02');
      expect(ajuste.horarioRegistrado, '08:47');
      expect(ajuste.justificativa, 'O app não abriu na portaria.');
      expect(ajuste.data, DateTime(2026, 10, 20));
      expect(ajuste.enviadoEm, DateTime(2026, 10, 25, 8, 30));
      expect(ajuste.situacao, SituacaoAjuste.pendente);
      expect(ajuste.colaborador.matricula, 'FUNC-0001');
    });

    test('justificativa curta, horário inválido e texto longo demais não chamam a API', () async {
      var chamadas = 0;
      final r = repo(MockClient((_) async {
        chamadas++;
        return resposta(manutencao(), 201);
      }));

      Future<void> pedir(String horario, String texto) => r.solicitarAjuste(
            data: DateTime(2026, 10, 20),
            tipoBatida: TipoBatida.saida,
            horarioSolicitado: horario,
            justificativa: texto,
          );

      await expectLater(pedir('17:00', 'curto'), throwsA(isA<Falha>()));
      await expectLater(
        pedir('25:99', 'Justificativa com tamanho suficiente.'),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('HH:mm'))),
      );
      await expectLater(pedir('17:00', 'x' * 500), throwsA(isA<Falha>()));
      expect(chamadas, 0);
    });

    test('recusa da API chega com a mensagem do ProblemDetails', () async {
      final r = repo(MockClient((_) async => http.Response(
            jsonEncode({'title': 'Conflito', 'status': 409, 'detail': 'Já existe um pedido de manutenção pendente para este dia.'}),
            409,
            headers: {'content-type': 'application/problem+json; charset=utf-8'},
          )));

      await expectLater(
        r.solicitarAjuste(
          data: DateTime(2026, 10, 20),
          tipoBatida: TipoBatida.entrada,
          horarioSolicitado: '08:00',
          justificativa: 'Esqueci de bater o ponto.',
        ),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 409)),
      );
    });
  });

  group('justificativa com prefixo', () {
    test('codifica e decodifica o tipo e o horário', () {
      for (final tipo in TipoBatida.values) {
        final texto = PontoRepositoryApi.codificarJustificativa(tipo: tipo, horario: '13:05', texto: 'Motivo qualquer.');
        final lido = PontoRepositoryApi.decodificarJustificativa(texto);
        expect(lido.tipo, tipo);
        expect(lido.horario, '13:05');
        expect(lido.texto, 'Motivo qualquer.');
      }
    });

    test('pedido sem prefixo (feito fora do app) vira a primeira batida que falta', () {
      final ajuste = PontoRepositoryApi.ajusteDeJson(manutencao(
        justificativa: 'Esqueci de bater a saída.',
        batidas: const [
          {'tipo': 'Entrada', 'nome': 'entrada', 'horario': '08:00:00'},
          {'tipo': 'SaidaIntervalo', 'nome': 'saída para o intervalo', 'horario': '12:00:00'},
        ],
      ));

      expect(ajuste.tipoBatida, TipoBatida.retornoIntervalo);
      expect(ajuste.horarioSolicitado, '—');
      expect(ajuste.horarioRegistrado, isNull);
      expect(ajuste.justificativa, 'Esqueci de bater a saída.');
    });

    test('status da API viram a situação do app', () {
      SituacaoAjuste de(String s) => PontoRepositoryApi.ajusteDeJson(manutencao(status: s)).situacao;

      expect(de('Pendente'), SituacaoAjuste.pendente);
      expect(de('Aprovada'), SituacaoAjuste.aprovado);
      expect(de('Rejeitada'), SituacaoAjuste.recusado);
      expect(() => de('Cancelada'), throwsA(isA<ApiException>()));
    });
  });

  group('carregarPonto', () {
    final hoje = {
      'data': '2026-10-21',
      'horaServidor': '2026-10-21T10:30:15.1234567-03:00',
      'batidas': [
        {'tipo': 'Entrada', 'nome': 'entrada', 'horario': '08:05:00'},
      ],
      'proximaBatida': 'SaidaIntervalo',
    };
    final espelho = {'dias': <Object>[], 'saldoBancoHorasHoras': 0};

    test('traz os meus pedidos de ajuste (escopo minhas)', () async {
      late Map<String, String> consulta;
      final r = repo(MockClient((req) async {
        switch (req.url.path) {
          case '/api/v1/ponto/hoje':
            return resposta(hoje);
          case '/api/v1/ponto/espelho':
            return resposta(espelho);
          case '/api/v1/manutencoes-ponto':
            consulta = req.url.queryParameters;
            return resposta({
              'itens': [manutencao(id: 9, status: 'Aprovada'), manutencao(id: 8)],
              'pagina': 1,
              'tamanho': 20,
              'total': 2,
            });
        }
        return http.Response('', 404);
      }));

      final e = await r.carregarPonto();

      expect(consulta['escopo'], 'minhas');
      expect(e.minhasSolicitacoes.map((a) => a.id), ['9', '8']);
      expect(e.minhasSolicitacoes.first.situacao, SituacaoAjuste.aprovado);
    });

    test('se a lista de pedidos falhar, o espelho abre mesmo assim', () async {
      final r = repo(MockClient((req) async {
        if (req.url.path == '/api/v1/ponto/hoje') return resposta(hoje);
        if (req.url.path == '/api/v1/ponto/espelho') return resposta(espelho);
        return http.Response('', 500);
      }));

      final e = await r.carregarPonto();

      expect(e.hoje!.batidas.single.tipo, TipoBatida.entrada);
      expect(e.minhasSolicitacoes, isEmpty);
    });
  });
}
