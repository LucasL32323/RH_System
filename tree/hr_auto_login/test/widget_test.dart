import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/app.dart';

/// Teste de fumaça: o app sobe e abre na autenticação (sem sessão).
void main() {
  testWidgets('app abre na tela de login', (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: HrAutoApp(usarGoogleFonts: false)));
    await tester.pumpAndSettle();

    expect(find.text('Bem-vindo de volta'), findsOneWidget);
    expect(find.byType(FilledButton), findsWidgets);
  });
}
