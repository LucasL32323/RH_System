import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/core/network/api_client.dart';
import 'package:hr_auto/data/models/status_sistema.dart';
import 'package:hr_auto/data/repositories/sistema_repository.dart';
import 'package:hr_auto/presentation/pages/fundacao_page.dart';
import 'package:hr_auto/presentation/viewmodels/status_view_model.dart';

/// Repositório falso: devolve sucesso ou lança erro, sem rede.
class _RepositorioFake implements SistemaRepository {
  _RepositorioFake({this.falhar = false});

  final bool falhar;

  @override
  Future<StatusSistema> obterStatus() async {
    if (falhar) throw ApiException('Não foi possível conectar à API.');
    return StatusSistema(
      nome: 'HR-Auto API',
      versao: '0.1.0',
      ambiente: 'Test',
      horaServidor: DateTime(2026, 10, 15, 12),
    );
  }
}

Widget _app(SistemaRepository repositorio) => ProviderScope(
      overrides: [sistemaRepositoryProvider.overrideWithValue(repositorio)],
      child: const MaterialApp(home: FundacaoPage()),
    );

void main() {
  testWidgets('mostra "API conectada" quando a API responde', (tester) async {
    await tester.pumpWidget(_app(_RepositorioFake()));
    await tester.pumpAndSettle();

    expect(find.text('API conectada'), findsOneWidget);
    expect(find.textContaining('HR-Auto API v0.1.0'), findsOneWidget);
  });

  testWidgets('mostra a mensagem de erro quando a API falha', (tester) async {
    await tester.pumpWidget(_app(_RepositorioFake(falhar: true)));
    await tester.pumpAndSettle();

    expect(find.text('Não foi possível conectar à API.'), findsOneWidget);
    expect(find.text('Testar conexão'), findsOneWidget);
  });

  testWidgets('menu lateral tem só ícone e tooltip', (tester) async {
    await tester.pumpWidget(_app(_RepositorioFake()));
    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.home_outlined), findsOneWidget);
    expect(find.byTooltip('Início'), findsOneWidget);
    expect(find.text('Início'), findsNothing);
  });
}
