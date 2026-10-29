import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme/app_theme.dart';
import 'domain/entities/usuario_autenticado.dart';
import 'presentation/pages/admin/admin_page.dart';
import 'presentation/pages/auth_page.dart';
import 'presentation/pages/home_page.dart';
import 'presentation/pages/perfil/area_perfil_page.dart';
import 'presentation/providers.dart';

/// Raiz do app: tema e escolha da tela pela sessão
/// (sem sessão -> autenticação; Admin -> área do administrador; RH e Colaborador -> área do perfil;
/// qualquer outro valor de perfil -> tela provisória).
class HrAutoApp extends StatelessWidget {
  const HrAutoApp({super.key, this.usarGoogleFonts = true});

  /// false nos testes de widget (sem internet para baixar a fonte Inter).
  final bool usarGoogleFonts;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'HR-Auto',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.claro(usarGoogleFonts: usarGoogleFonts),
      home: const _RaizSessao(),
    );
  }
}

class _RaizSessao extends ConsumerWidget {
  const _RaizSessao();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessao = ref.watch(sessaoProvider);
    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 400),
      child: switch (sessao) {
        null => const AuthPage(key: ValueKey('auth')),
        UsuarioAutenticado(perfil: 'Admin') => AdminPage(key: const ValueKey('admin'), usuario: sessao),
        // A chave inclui o código: sair e entrar com outra conta recomeça no Início.
        UsuarioAutenticado(perfil: 'RH' || 'Colaborador') =>
          AreaPerfilPage(key: ValueKey('perfil_${sessao.codigo}'), usuario: sessao),
        _ => HomePage(key: const ValueKey('home'), usuario: sessao),
      },
    );
  }
}
