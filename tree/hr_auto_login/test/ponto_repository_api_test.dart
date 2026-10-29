import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/data/repositories/auth_repository_api.dart';
import 'package:hr_auto/data/repositories/ponto_repository_api.dart';
import 'package:hr_auto/domain/entities/ponto.dart';

/// Integração Flutter x API .NET sem rede: o MockClient responde como os endpoints da Fase 4
/// (/api/v1/ponto/hoje, /api/v1/ponto/espelho e POST /api/v1/ponto/batidas).
void main() {
  const token = 'jwt-de-teste';

  // Quarta-feira, 21/10/2026, 10:30 em Brasília. Segunda e terça fechadas; hoje só a entrada.
  final hoje = {
    'data': '2026-10-21',
    'horaServidor': '2026-10-21T10:30:15.1234567-03:00',
    'batidas': [
      {'tipo': 'Entrada', 'nome': 'entrada', 'horario': '08:05:00'},
    ],
    'proximaBatida': 'SaidaIntervalo',
    'proximaBatidaNome': 'saída para o intervalo',
  };

  Map<String, dynamic> dia(String data, String? e, String? si, String? ri, String? s,
          {int trabalhado = 0, int extras = 0, int faltantes = 0}) =>
      {
        'data': data,
        'contexto': 'DiaUtil',
        'situacao': 'Completo',
        'entrada': e,
        'saidaIntervalo': si,
        'retornoIntervalo': ri,
        'saida': s,
        'trabalhadoMinutos': trabalhado,
        'extrasMinutos': extras,
        'faltantesMinutos': faltantes,
        'atrasoMinutos': 0,
      };

  final espelho = {
    'codigo': 'FUNC-0001',
    'nome': 'Administrador do Sistema',
    'de': '2026-10-19',
    'ate': '2026-10-21',
    'dias': [
      // 08:02-12:01 + 13:00-17:10 = 8h09 -> 9 min extras sobre 8 h.
      dia('2026-10-19', '08:02:00', '12:01:00', '13:00:00', '17:10:00', trabalhado: 489, extras: 9),
      // 08:03-12:02 + 13:01-17:19 = 8h17 -> 17 min extras.
      dia('2026-10-20', '08:03:00', '12:02:00', '13:01:00', '17:19:00', trabalhado: 497, extras: 17),
      dia('2026-10-21', '08:05:00', null, null, null, trabalhado: 0),
    ],
    'totais': {
      'trabalhadoMinutos': 986,
      'extrasMinutos': 26,
      'faltantesMinutos': 0,
      'atrasosMinutos': 0,
      'diasTrabalhados': 2,
      'faltas': 0,
      'diasInconsistentes': 0,
      'saldoMinutos': 26,
    },
    'saldoBancoHorasHoras': 12.9333,
  };

  http.Response resposta(Object corpo, [int status = 200]) => http.Response(
        jsonEncode(corpo),
        status,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );

  group('PontoRepositoryApi', () {
    test('carregarPonto junta /ponto/hoje e /ponto/espelho da semana do servidor', () async {
      final chamadas = <http.Request>[];
      final cliente = MockClient((req) async {
        chamadas.add(req);
        if (req.url.path == '/api/v1/ponto/hoje') return resposta(hoje);
        if (req.url.path == '/api/v1/ponto/espelho') return resposta(espelho);
        return http.Response('', 404);
      });
      final repo = PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => token);

      final e = await repo.carregarPonto();

      // Token em todas as chamadas e semana pedida de segunda até hoje (datas do servidor).
      expect(chamadas.every((r) => r.headers['Authorization'] == 'Bearer $token'), isTrue);
      final consulta = chamadas.firstWhere((r) => r.url.path == '/api/v1/ponto/espelho').url.queryParameters;
      expect(consulta, {'de': '2026-10-19', 'ate': '2026-10-21'});

      // Hora do servidor sem converter para o fuso do aparelho.
      expect(e.agora, DateTime(2026, 10, 21, 10, 30, 15));
      expect(e.semana.map((d) => d.data.day), [19, 20, 21, 22, 23]);
      expect(e.hoje!.batidas.single.tipo, TipoBatida.entrada);
      expect(e.hoje!.batidas.single.horario, DateTime(2026, 10, 21, 8, 5));
      expect(e.proximaBatida, TipoBatida.saidaIntervalo);

      // Saldo do dia igual ao da API (carga deduzida dos totais) e banco de horas em minutos.
      expect(e.semana[0].saldo, const Duration(minutes: 9));
      expect(e.semana[1].saldo, const Duration(minutes: 17));
      expect(e.semana[3].batidas, isEmpty); // quinta ainda não chegou
      expect(e.saldoBancoHoras, const Duration(hours: 12, minutes: 56));
      expect(e.minhasSolicitacoes, isEmpty);
    });

    test('registrarBatida faz POST com token e devolve a batida com o horário do servidor', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return resposta({
          'tipo': 'SaidaIntervalo',
          'nome': 'saída para o intervalo',
          'data': '2026-10-21',
          'horario': '12:00:42',
          'proximaBatida': 'RetornoIntervalo',
          'fechamentoDoDia': null,
        }, 201);
      });
      final repo = PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => token);

      final batida = await repo.registrarBatida();

      expect(enviado.method, 'POST');
      expect(enviado.url.path, '/api/v1/ponto/batidas');
      expect(enviado.headers['Authorization'], 'Bearer $token');
      expect(batida.tipo, TipoBatida.saidaIntervalo);
      expect(batida.horario, DateTime(2026, 10, 21, 12, 0, 42));
    });

    test('recusa do servidor chega com a mensagem do ProblemDetails', () async {
      final cliente = MockClient((_) async => http.Response(
            jsonEncode({'title': 'Conflito', 'status': 409, 'detail': 'As quatro batidas de hoje já foram registradas.'}),
            409,
            headers: {'content-type': 'application/problem+json; charset=utf-8'},
          ));
      final repo = PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => token);

      await expectLater(
        repo.registrarBatida(),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'status', 409)
            .having((e) => e.mensagem, 'mensagem', 'As quatro batidas de hoje já foram registradas.')),
      );
    });

    test('401 com token vira "sessão expirou" (e não "senha inválida")', () async {
      final cliente = MockClient((_) async => http.Response('', 401));
      final repo = PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => token);

      await expectLater(
        repo.carregarPonto(),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', 'Sua sessão expirou. Entre novamente.')),
      );
    });

    test('sem sessão não chama a API', () async {
      var chamou = false;
      final cliente = MockClient((_) async {
        chamou = true;
        return resposta(hoje);
      });
      final repo = PontoRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'), token: () => null);

      await expectLater(repo.carregarPonto(), throwsA(isA<Falha>()));
      expect(chamou, isFalse);
    });
  });

  group('AuthRepositoryApi', () {
    test('login com e-mail chama POST /auth/login e monta o usuário', () async {
      late Map<String, dynamic> corpo;
      final cliente = MockClient((req) async {
        corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return resposta({
          'token': token,
          'expiraEm': '2026-10-21T18:30:00-03:00',
          'usuario': {'codigo': 'FUNC-0001', 'nome': 'Administrador do Sistema', 'email': 'admin@hrauto.local', 'perfil': 'Admin'},
        });
      });
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      final usuario = await repo.entrar(login: ' admin@hrauto.local ', senha: 'Admin@123');

      expect(corpo, {'email': 'admin@hrauto.local', 'senha': 'Admin@123'});
      expect(usuario.codigo, 'FUNC-0001');
      expect(usuario.perfil, 'Admin');
      expect(usuario.token, token);
    });

    test('senha errada mostra a mensagem genérica', () async {
      final cliente = MockClient((_) async => http.Response('', 401));
      final repo = AuthRepositoryApi(ApiClient(cliente: cliente, baseUrl: 'http://api'));

      await expectLater(
        repo.entrar(login: 'admin@hrauto.local', senha: 'Errada@123'),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', 'E-mail ou senha inválidos.')),
      );
    });
  });
}
