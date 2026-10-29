import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hr_auto/core/errors/falha.dart';
import 'package:hr_auto/core/files/arquivos.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/core/theme/app_theme.dart';
import 'package:hr_auto/data/repositories/holerite_repository_api.dart';
import 'package:hr_auto/data/repositories/holerite_repository_prototipo.dart';
import 'package:hr_auto/domain/entities/usuario_autenticado.dart';
import 'package:hr_auto/domain/regras/regras_holerite.dart';
import 'package:hr_auto/presentation/pages/admin/aba_holerites.dart';
import 'package:hr_auto/presentation/providers.dart';
import 'package:hr_auto/presentation/viewmodels/holerites_view_model.dart';

/// Holerites (RF29, RF30): regras, repositório da API (MockClient), repositório em memória,
/// ViewModels e a aba. Sem rede, sem plugins (seletor e salvador de arquivos são falsos) e com relógio fixo.
void main() {
  const token = 'jwt-de-teste';
  final agora = DateTime(2026, 10, 27, 12); // terça-feira
  final pdf = HoleriteRepositoryPrototipo.gerarPdfDeExemplo('Teste');

  UsuarioAutenticado usuario(String perfil, {String codigo = 'FUNC-0002', String nome = 'Renata Lopes'}) =>
      UsuarioAutenticado(
        token: token,
        expiraEm: DateTime(2030),
        codigo: codigo,
        nome: nome,
        email: 'usuario@hrauto.local',
        perfil: perfil,
      );

  HoleriteRepositoryPrototipo repositorio(UsuarioAutenticado u) => HoleriteRepositoryPrototipo(
        codigoUsuario: u.codigo,
        nomeUsuario: u.nome,
        perfilUsuario: u.perfil,
        relogio: () => agora,
      );

  http.Response json(Object corpo, [int status = 200]) => http.Response(
        jsonEncode(corpo),
        status,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );

  Map<String, dynamic> holeriteJson({int id = 7, String codigo = 'FUNC-0004', int mes = 9, int ano = 2026}) => {
        'id': id,
        'codigoFuncionario': codigo,
        'nomeFuncionario': 'Ana Souza',
        'mes': mes,
        'ano': ano,
        'dataPublicacao': '2026-10-05',
        'nomeArquivo': 'holerite-$ano-${mes.toString().padLeft(2, '0')}.pdf',
      };

  HoleriteRepositoryApi repoApi(MockClient cliente, {String? Function()? sessao}) => HoleriteRepositoryApi(
        ApiClient(cliente: cliente, baseUrl: 'http://api'),
        token: sessao ?? () => token,
      );

  group('RegrasHolerite', () {
    test('normalizarCodigo aceita FUNC-0004 em qualquer caixa ou só o número', () {
      expect(RegrasHolerite.normalizarCodigo('func-0004'), 'FUNC-0004');
      expect(RegrasHolerite.normalizarCodigo(' FUNC-0004 '), 'FUNC-0004');
      expect(RegrasHolerite.normalizarCodigo('4'), 'FUNC-0004');
      expect(RegrasHolerite.normalizarCodigo(' 12 '), 'FUNC-0012');
      expect(RegrasHolerite.normalizarCodigo('0'), isNull);
      expect(RegrasHolerite.normalizarCodigo('FUNC-12'), isNull);
      expect(RegrasHolerite.normalizarCodigo('abc'), isNull);
      expect(RegrasHolerite.normalizarCodigo(''), isNull);
      expect(RegrasHolerite.normalizarCodigo(null), isNull);
    });

    test('pareceUmPdf confere a assinatura %PDF-', () {
      expect(RegrasHolerite.pareceUmPdf(pdf), isTrue);
      expect(RegrasHolerite.pareceUmPdf(Uint8List.fromList(utf8.encode('<html>erro</html>'))), isFalse);
      expect(RegrasHolerite.pareceUmPdf(Uint8List(3)), isFalse);
    });

    test('validarArquivo recusa vazio, grande demais, não-PDF e extensão errada', () {
      expect(() => RegrasHolerite.validarArquivo('h.pdf', pdf), returnsNormally);
      expect(
        () => RegrasHolerite.validarArquivo('h.pdf', Uint8List(0)),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'O arquivo do holerite está vazio.')),
      );
      expect(
        () => RegrasHolerite.validarArquivo('h.pdf', Uint8List(RegrasHolerite.tamanhoMaximoBytes + 1)),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('5 MB'))),
      );
      expect(
        () => RegrasHolerite.validarArquivo('h.txt', pdf),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'O holerite deve ser um arquivo PDF.')),
      );
      expect(
        () => RegrasHolerite.validarArquivo('h.pdf', Uint8List.fromList(utf8.encode('não sou um pdf'))),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'O arquivo escolhido não é um PDF válido.')),
      );
    });

    test('validarPublicacao devolve o código normalizado e confere mês e ano', () {
      String validar({String codigo = '4', int mes = 9, int ano = 2026}) => RegrasHolerite.validarPublicacao(
            codigo: codigo,
            mes: mes,
            ano: ano,
            nomeArquivo: 'h.pdf',
            conteudo: pdf,
          );

      expect(validar(), 'FUNC-0004');
      expect(() => validar(codigo: 'xx'), throwsA(isA<Falha>()));
      expect(() => validar(mes: 13), throwsA(isA<Falha>()));
      expect(() => validar(mes: 0), throwsA(isA<Falha>()));
      expect(() => validar(ano: 1999), throwsA(isA<Falha>()));
    });

    test('tamanhoLegivel', () {
      expect(RegrasHolerite.tamanhoLegivel(512), '512 B');
      expect(RegrasHolerite.tamanhoLegivel(340 * 1024), '340 KB');
      expect(RegrasHolerite.tamanhoLegivel(1572864), '1,5 MB');
    });
  });

  group('ApiClient (arquivos)', () {
    test('getBytes devolve o corpo sem interpretar e manda o token', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return http.Response.bytes(pdf, 200, headers: {'content-type': 'application/pdf'});
      });

      final bytes = await ApiClient(cliente: cliente, baseUrl: 'http://api').getBytes('/api/v1/holerites/7/arquivo', token: token);

      expect(bytes, pdf);
      expect(enviado.headers['Authorization'], 'Bearer $token');
      expect(enviado.headers['Accept'], contains('application/pdf'));
    });

    test('getBytes mostra a mensagem do ProblemDetails quando o servidor recusa', () async {
      final cliente = MockClient((_) async => json({'title': 'Não encontrado', 'detail': 'Holerite não encontrado.'}, 404));

      await expectLater(
        ApiClient(cliente: cliente, baseUrl: 'http://api').getBytes('/api/v1/holerites/9/arquivo', token: token),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'status', 404)
            .having((e) => e.mensagem, 'mensagem', 'Holerite não encontrado.')),
      );
    });

    test('postMultipart envia campos e arquivo e devolve o JSON', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return json({'ok': true}, 201);
      });

      final resposta = await ApiClient(cliente: cliente, baseUrl: 'http://api').postMultipart(
        '/api/v1/holerites',
        campos: {'Codigo': 'FUNC-0004'},
        campoArquivo: 'Arquivo',
        nomeArquivo: 'h.pdf',
        bytes: pdf,
        token: token,
      );

      expect(resposta, {'ok': true});
      expect(enviado.method, 'POST');
      expect(enviado.headers['content-type'], startsWith('multipart/form-data; boundary='));
      expect(enviado.headers['Authorization'], 'Bearer $token');
      expect(enviado.body, contains('name="Codigo"'));
      expect(enviado.body, contains('name="Arquivo"; filename="h.pdf"'));
      expect(enviado.body, contains('%PDF-'));
    });

    test('postMultipart: 413 vira mensagem de arquivo grande demais', () async {
      final cliente = MockClient((_) async => http.Response('', 413));

      await expectLater(
        ApiClient(cliente: cliente, baseUrl: 'http://api').postMultipart(
          '/api/v1/holerites',
          campos: const {},
          campoArquivo: 'Arquivo',
          nomeArquivo: 'h.pdf',
          bytes: pdf,
          token: token,
        ),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', 'O arquivo enviado é grande demais.')),
      );
    });
  });

  group('HoleriteRepositoryApi', () {
    test('listar sem código traz os holerites de quem está logado', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return json([holeriteJson(), holeriteJson(id: 6, mes: 8)]);
      });

      final lista = await repoApi(cliente).listar();

      expect(enviado.url.path, '/api/v1/holerites');
      expect(enviado.url.queryParameters, isEmpty);
      expect(enviado.headers['Authorization'], 'Bearer $token');
      expect(lista.length, 2);
      expect(lista.first.competencia, '09/2026');
      expect(lista.first.dataPublicacao, DateTime(2026, 10, 5));
      expect(lista.first.nomeFuncionario, 'Ana Souza');
    });

    test('listar por código normaliza e manda o ano', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return json(<Object>[]);
      });

      final lista = await repoApi(cliente).listar(codigo: '4', ano: 2026);

      expect(enviado.url.queryParameters, {'codigo': 'FUNC-0004', 'ano': '2026'});
      expect(lista, isEmpty);
    });

    test('código inválido e sessão ausente não chamam a API', () async {
      var chamou = false;
      final cliente = MockClient((_) async {
        chamou = true;
        return json(<Object>[]);
      });

      await expectLater(repoApi(cliente).listar(codigo: 'abc'), throwsA(isA<Falha>()));
      await expectLater(repoApi(cliente, sessao: () => null).listar(), throwsA(isA<Falha>()));
      expect(chamou, isFalse);
    });

    test('resposta fora do formato vira mensagem para o usuário', () async {
      final cliente = MockClient((_) async => json([{'id': 'não é número'}]));

      await expectLater(
        repoApi(cliente).listar(),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', 'Resposta da API em formato inesperado.')),
      );
    });

    test('baixar devolve o PDF com o nome do holerite', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return http.Response.bytes(pdf, 200, headers: {'content-type': 'application/pdf'});
      });
      final repo = repoApi(cliente);
      final holerite = HoleriteRepositoryApi.holeriteDeJson(holeriteJson());

      final arquivo = await repo.baixar(holerite);

      expect(enviado.url.path, '/api/v1/holerites/7/arquivo');
      expect(arquivo.nome, 'holerite-2026-09.pdf');
      expect(arquivo.bytes, pdf);
    });

    test('baixar recusa resposta que não é PDF (página de erro de proxy, por exemplo)', () async {
      final cliente = MockClient((_) async => http.Response('<html>Bad gateway</html>', 200));

      await expectLater(
        repoApi(cliente).baixar(HoleriteRepositoryApi.holeriteDeJson(holeriteJson())),
        throwsA(isA<ApiException>().having((e) => e.mensagem, 'mensagem', contains('PDF válido'))),
      );
    });

    test('publicar envia os campos do formulário e lê o resultado (201)', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return json({'holerite': holeriteJson(), 'substituido': false}, 201);
      });

      final resultado = await repoApi(cliente).publicar(
        codigo: 'func-0004',
        mes: 9,
        ano: 2026,
        nomeArquivo: 'holerite.pdf',
        conteudo: pdf,
      );

      expect(resultado.substituido, isFalse);
      expect(resultado.holerite.codigoFuncionario, 'FUNC-0004');
      expect(enviado.url.path, '/api/v1/holerites');
      expect(enviado.body, contains('name="Codigo"'));
      expect(enviado.body, contains('FUNC-0004'));
      expect(enviado.body, contains('name="Mes"'));
      expect(enviado.body, contains('name="Ano"'));
      expect(enviado.body, contains('name="Arquivo"; filename="holerite.pdf"'));
      expect(enviado.body, isNot(contains('name="Substituir"')));
    });

    test('publicar com substituir manda o campo Substituir e lê o 200', () async {
      late http.Request enviado;
      final cliente = MockClient((req) async {
        enviado = req;
        return json({'holerite': holeriteJson(), 'substituido': true});
      });

      final resultado = await repoApi(cliente).publicar(
        codigo: 'FUNC-0004',
        mes: 9,
        ano: 2026,
        nomeArquivo: 'holerite.pdf',
        conteudo: pdf,
        substituir: true,
      );

      expect(resultado.substituido, isTrue);
      expect(enviado.body, contains('name="Substituir"'));
    });

    test('publicar confere o arquivo antes de enviar e repassa o conflito da API', () async {
      var chamadas = 0;
      final cliente = MockClient((_) async {
        chamadas++;
        return json({'title': 'Conflito', 'detail': 'Já existe holerite de 09/2026 para FUNC-0004.'}, 409);
      });
      final repo = repoApi(cliente);

      await expectLater(
        repo.publicar(
          codigo: 'FUNC-0004',
          mes: 9,
          ano: 2026,
          nomeArquivo: 'holerite.pdf',
          conteudo: Uint8List.fromList(utf8.encode('não é pdf')),
        ),
        throwsA(isA<Falha>()),
      );
      expect(chamadas, 0);

      await expectLater(
        repo.publicar(codigo: 'FUNC-0004', mes: 9, ano: 2026, nomeArquivo: 'holerite.pdf', conteudo: pdf),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'status', 409)
            .having((e) => e.mensagem, 'mensagem', startsWith('Já existe'))),
      );
      expect(chamadas, 1);
    });
  });

  group('HoleriteRepositoryPrototipo', () {
    test('o usuário começa com os três meses anteriores, do mais recente ao mais antigo', () async {
      final repo = repositorio(usuario('Colaborador'));

      final lista = await repo.listar();

      expect(lista.map((h) => h.competencia), ['09/2026', '08/2026', '07/2026']);
      expect(lista.every((h) => h.codigoFuncionario == 'FUNC-0002'), isTrue);
      expect(lista.every((h) => !h.dataPublicacao.isAfter(agora)), isTrue);
      expect(await repo.listar(ano: 2025), isEmpty);
    });

    test('colaborador só vê os próprios; RH consulta por código e o inexistente dá erro', () async {
      final colaborador = repositorio(usuario('Colaborador'));
      await expectLater(
        colaborador.listar(codigo: 'FUNC-0004'),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'Você só pode ver os seus próprios holerites.')),
      );

      final rh = repositorio(usuario('RH'));
      expect(await rh.listar(codigo: '4'), isEmpty); // Ana Souza existe e ainda não tem holerite
      await expectLater(
        rh.listar(codigo: 'FUNC-0099'),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'Funcionário não encontrado.')),
      );
    });

    test('publicar: novo, conflito sem substituir e substituição mantêm a mesma competência única', () async {
      final repo = repositorio(usuario('RH'));

      final novo = await repo.publicar(codigo: '4', mes: 9, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf);
      expect(novo.substituido, isFalse);
      expect(novo.holerite.nomeFuncionario, 'Ana Souza');

      await expectLater(
        repo.publicar(codigo: 'FUNC-0004', mes: 9, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', startsWith('Já existe'))),
      );

      final troca = await repo.publicar(
        codigo: 'FUNC-0004',
        mes: 9,
        ano: 2026,
        nomeArquivo: 'h.pdf',
        conteudo: pdf,
        substituir: true,
      );
      expect(troca.substituido, isTrue);
      expect(troca.holerite.id, novo.holerite.id);
      expect((await repo.listar(codigo: 'FUNC-0004')).length, 1);
    });

    test('regras do servidor: perfil, próprio holerite e mês futuro', () async {
      await expectLater(
        repositorio(usuario('Colaborador')).publicar(codigo: '4', mes: 9, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'Apenas o RH pode publicar holerites.')),
      );

      final rh = repositorio(usuario('RH'));
      await expectLater(
        rh.publicar(codigo: 'FUNC-0002', mes: 9, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', 'Você não pode publicar o seu próprio holerite.')),
      );
      await expectLater(
        rh.publicar(codigo: 'FUNC-0004', mes: 11, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('ainda não começou'))),
      );
    });

    test('baixar entrega o PDF publicado e recusa holerite que não existe', () async {
      final repo = repositorio(usuario('RH'));
      final publicado = await repo.publicar(codigo: '4', mes: 9, ano: 2026, nomeArquivo: 'h.pdf', conteudo: pdf);

      final arquivo = await repo.baixar(publicado.holerite);
      expect(arquivo.bytes, pdf);
      expect(arquivo.nome, 'holerite-2026-09.pdf');

      final inexistente = HoleriteRepositoryApi.holeriteDeJson(holeriteJson(id: 999));
      await expectLater(repo.baixar(inexistente), throwsA(isA<Falha>()));
    });

    test('o PDF de exemplo tem xref coerente (abre em qualquer leitor)', () {
      final texto = ascii.decode(HoleriteRepositoryPrototipo.gerarPdfDeExemplo('Holerite 09/2026 - FUNC-0002'));
      final inicio = int.parse(RegExp(r'startxref\n(\d+)\n').firstMatch(texto)!.group(1)!);

      expect(texto.startsWith('%PDF-1.4'), isTrue);
      expect(texto.substring(inicio).startsWith('xref'), isTrue);
      expect(texto.trimRight().endsWith('%%EOF'), isTrue);
    });
  });

  group('ViewModels', () {
    ProviderContainer criar(UsuarioAutenticado u, {_SalvadorFalso? salvador, SeletorArquivo? seletor}) {
      final container = ProviderContainer(overrides: [
        sessaoProvider.overrideWith((ref) => u),
        holeriteRepositoryProvider.overrideWithValue(repositorio(u)),
        salvadorArquivoProvider.overrideWithValue(salvador ?? _SalvadorFalso()),
        seletorArquivoProvider.overrideWithValue(seletor ?? _SeletorFalso()),
      ]);
      addTearDown(container.dispose);
      return container;
    }

    test('"Meus holerites" carrega a lista e baixa o PDF pelo salvador', () async {
      final salvador = _SalvadorFalso();
      final c = criar(usuario('Colaborador'), salvador: salvador);

      final lista = await c.read(holeritesViewModelProvider.future);
      expect(lista.map((h) => h.competencia), ['09/2026', '08/2026', '07/2026']);

      final onde = await c.read(holeritesViewModelProvider.notifier).baixar(lista.first);

      expect(onde, 'holerite-2026-09.pdf');
      expect(salvador.nomes, ['holerite-2026-09.pdf']);
      expect(RegrasHolerite.pareceUmPdf(salvador.conteudos.single), isTrue);
    });

    test('consulta da equipe: código inválido vira erro; válido traz a lista', () async {
      final c = criar(usuario('RH'));
      await c.read(consultaHoleritesViewModelProvider.future);
      final consulta = c.read(consultaHoleritesViewModelProvider.notifier);

      await consulta.consultar('abc');
      expect(c.read(consultaHoleritesViewModelProvider).error, isA<Falha>());

      await consulta.consultar('4');
      final resultado = c.read(consultaHoleritesViewModelProvider).value!;
      expect(resultado.codigo, 'FUNC-0004');
      expect(resultado.holerites, isEmpty);
      expect(resultado.nome, '');

      consulta.limpar();
      expect(c.read(consultaHoleritesViewModelProvider).value, isNull);
    });

    test('publicar atualiza a consulta aberta do mesmo funcionário', () async {
      final c = criar(usuario('RH'));
      await c.read(consultaHoleritesViewModelProvider.future);
      await c.read(consultaHoleritesViewModelProvider.notifier).consultar('FUNC-0004');

      final resultado = await c.read(publicarHoleriteViewModelProvider.notifier).publicar(
            codigo: '4',
            mes: 9,
            ano: 2026,
            arquivo: ArquivoSelecionado(nome: 'h.pdf', bytes: pdf),
          );

      expect(resultado.substituido, isFalse);
      expect(c.read(publicarHoleriteViewModelProvider), isFalse);
      final consulta = c.read(consultaHoleritesViewModelProvider).value!;
      expect(consulta.holerites.map((h) => h.competencia), ['09/2026']);
      expect(consulta.nome, 'Ana Souza');
    });

    test('publicar repetido sem substituir falha; com substituir troca o PDF', () async {
      final c = criar(usuario('RH'));
      final publicar = c.read(publicarHoleriteViewModelProvider.notifier);
      final arquivo = ArquivoSelecionado(nome: 'h.pdf', bytes: pdf);

      await publicar.publicar(codigo: '4', mes: 9, ano: 2026, arquivo: arquivo);
      await expectLater(
        publicar.publicar(codigo: '4', mes: 9, ano: 2026, arquivo: arquivo),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', startsWith('Já existe'))),
      );
      expect(c.read(publicarHoleriteViewModelProvider), isFalse); // liberou o botão mesmo com erro

      final troca = await publicar.publicar(codigo: '4', mes: 9, ano: 2026, arquivo: arquivo, substituir: true);
      expect(troca.substituido, isTrue);
    });

    test('segundo clique durante o envio é recusado', () async {
      final c = criar(usuario('RH'));
      final publicar = c.read(publicarHoleriteViewModelProvider.notifier);
      final arquivo = ArquivoSelecionado(nome: 'h.pdf', bytes: pdf);

      final primeiro = publicar.publicar(codigo: '4', mes: 9, ano: 2026, arquivo: arquivo);
      await expectLater(
        publicar.publicar(codigo: '4', mes: 8, ano: 2026, arquivo: arquivo),
        throwsA(isA<Falha>().having((f) => f.mensagem, 'mensagem', contains('em andamento'))),
      );
      await primeiro;
      expect(c.read(publicarHoleriteViewModelProvider), isFalse);
    });
  });

  group('AbaHolerites', () {
    Future<_Cena> abrir(
      WidgetTester tester,
      String perfil, {
      ArquivoSelecionado? arquivo,
      Falha? erroSeletor,
    }) async {
      tester.view.physicalSize = const Size(1440, 1800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      final u = usuario(perfil);
      final salvador = _SalvadorFalso();
      final seletor = _SeletorFalso(arquivo: arquivo, erro: erroSeletor);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            sessaoProvider.overrideWith((ref) => u),
            holeriteRepositoryProvider.overrideWithValue(repositorio(u)),
            salvadorArquivoProvider.overrideWithValue(salvador),
            seletorArquivoProvider.overrideWithValue(seletor),
          ],
          child: MaterialApp(
            theme: AppTheme.claro(usarGoogleFonts: false),
            home: Scaffold(body: AbaHolerites(usuario: u)),
          ),
        ),
      );
      await tester.pumpAndSettle();
      return _Cena(salvador, seletor);
    }

    Future<void> tocar(WidgetTester tester, Finder alvo) async {
      await tester.ensureVisible(alvo);
      await tester.pumpAndSettle();
      await tester.tap(alvo);
      await tester.pumpAndSettle();
    }

    Future<void> digitar(WidgetTester tester, String campo, String texto) async {
      final alvo = find.byKey(Key('campo_$campo'));
      await tester.ensureVisible(alvo);
      await tester.enterText(alvo, texto);
      await tester.pumpAndSettle();
    }

    Future<void> escolherMes(WidgetTester tester, String mes) async {
      await tocar(tester, find.byKey(const Key('campo_Mês')));
      await tocar(tester, find.text(mes).last);
    }

    testWidgets('colaborador vê só "Meus holerites" e baixa o PDF', (tester) async {
      final cena = await abrir(tester, 'Colaborador');

      expect(find.text('Meus holerites'), findsOneWidget);
      expect(find.text('Setembro de 2026'), findsOneWidget);
      expect(find.text('Agosto de 2026'), findsOneWidget);
      expect(find.text('Julho de 2026'), findsOneWidget);
      expect(find.text('Holerites da equipe'), findsNothing);
      expect(find.byKey(const Key('botao_publicar_holerite')), findsNothing);

      await tocar(tester, find.byKey(const Key('baixar_meus_1')));

      expect(cena.salvador.nomes, ['holerite-2026-09.pdf']);
      expect(find.text('Holerite 09/2026 baixado.'), findsOneWidget);
    });

    testWidgets('RH consulta pelo código FUNC: valida o formato e mostra o erro do servidor', (tester) async {
      await abrir(tester, 'RH');
      expect(find.text('Holerites da equipe'), findsOneWidget);
      expect(find.text('Digite o código do funcionário para ver os holerites publicados.'), findsOneWidget);

      await digitar(tester, 'Código FUNC', 'Ana');
      await tocar(tester, find.byKey(const Key('botao_consultar_holerites')));
      expect(find.text('Use o formato FUNC-0001 (ou só o número).'), findsOneWidget);

      await digitar(tester, 'Código FUNC', 'FUNC-0099');
      await tocar(tester, find.byKey(const Key('botao_consultar_holerites')));
      expect(find.text('Funcionário não encontrado.'), findsOneWidget);

      await digitar(tester, 'Código FUNC', '4');
      await tocar(tester, find.byKey(const Key('botao_consultar_holerites')));
      expect(find.text('FUNC-0004 ainda não tem holerites publicados.'), findsOneWidget);
    });

    testWidgets('RH publica, a consulta aberta se atualiza, repete e substitui', (tester) async {
      final cena = await abrir(
        tester,
        'RH',
        arquivo: ArquivoSelecionado(nome: 'holerite-ana.pdf', bytes: pdf),
      );

      // Consulta aberta de FUNC-0004 (ainda vazia).
      await digitar(tester, 'Código FUNC', '4');
      await tocar(tester, find.byKey(const Key('botao_consultar_holerites')));

      // Sem arquivo: pede o PDF.
      await digitar(tester, 'Código do funcionário', '4');
      await escolherMes(tester, 'Setembro');
      await digitar(tester, 'Ano', '2026');
      await tocar(tester, find.byKey(const Key('botao_publicar_holerite')));
      expect(find.text('Escolha o PDF do holerite.'), findsOneWidget);

      // Escolhe o PDF e publica.
      await tocar(tester, find.byKey(const Key('botao_escolher_pdf')));
      expect(cena.seletor.chamadas, 1);
      expect(find.textContaining('holerite-ana.pdf'), findsOneWidget);

      await tocar(tester, find.byKey(const Key('botao_publicar_holerite')));
      expect(find.text('Holerite 09/2026 de FUNC-0004 publicado.'), findsOneWidget);
      expect(find.text('Nenhum arquivo escolhido'), findsOneWidget);

      // A consulta aberta passou a mostrar o holerite novo (id 4: os ids 1 a 3 são do próprio RH).
      expect(find.byKey(const Key('baixar_equipe_4')), findsOneWidget);
      expect(find.text('Ana Souza · FUNC-0004'), findsOneWidget);
      await tocar(tester, find.byKey(const Key('baixar_equipe_4')));
      expect(cena.salvador.nomes, ['holerite-2026-09.pdf']);

      // Mesma competência de novo: conflito com a opção de substituir.
      await tocar(tester, find.byKey(const Key('botao_escolher_pdf')));
      await tocar(tester, find.byKey(const Key('botao_publicar_holerite')));
      expect(find.textContaining('Já existe holerite de 09/2026 para FUNC-0004'), findsOneWidget);

      await tocar(tester, find.byKey(const Key('botao_substituir_e_publicar')));
      expect(find.text('Holerite 09/2026 de FUNC-0004 substituído.'), findsOneWidget);
      expect(find.byKey(const Key('erro_publicar_holerite')), findsNothing);
    });

    testWidgets('falha do seletor de arquivos aparece no painel', (tester) async {
      await abrir(tester, 'Admin', erroSeletor: const Falha('Não foi possível abrir o seletor de arquivos neste aparelho.'));

      await tocar(tester, find.byKey(const Key('botao_escolher_pdf')));

      expect(find.text('Não foi possível abrir o seletor de arquivos neste aparelho.'), findsOneWidget);
    });

    testWidgets('arquivo que não é PDF é recusado já na escolha', (tester) async {
      await abrir(
        tester,
        'RH',
        arquivo: ArquivoSelecionado(nome: 'foto.pdf', bytes: Uint8List.fromList(utf8.encode('não sou um pdf'))),
      );

      await tocar(tester, find.byKey(const Key('botao_escolher_pdf')));

      expect(find.text('O arquivo escolhido não é um PDF válido.'), findsOneWidget);
      expect(find.text('Nenhum arquivo escolhido'), findsOneWidget);
    });
  });
}

/// Seletor de arquivos falso: devolve [arquivo] (ou null = cancelou) ou lança [erro].
class _SeletorFalso implements SeletorArquivo {
  _SeletorFalso({this.arquivo, this.erro});

  final ArquivoSelecionado? arquivo;
  final Falha? erro;
  int chamadas = 0;

  @override
  Future<ArquivoSelecionado?> escolherPdf() async {
    chamadas++;
    if (erro != null) throw erro!;
    return arquivo;
  }
}

/// Salvador falso: guarda o que seria baixado.
class _SalvadorFalso implements SalvadorArquivo {
  final List<String> nomes = [];
  final List<Uint8List> conteudos = [];

  @override
  Future<String> salvarPdf({required String nome, required Uint8List bytes}) async {
    nomes.add(nome);
    conteudos.add(bytes);
    return nome;
  }
}

class _Cena {
  const _Cena(this.salvador, this.seletor);

  final _SalvadorFalso salvador;
  final _SeletorFalso seletor;
}
