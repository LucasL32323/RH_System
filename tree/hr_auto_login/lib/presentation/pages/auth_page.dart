import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../widgets/cadastro_form.dart';
import '../widgets/cartao_formulario.dart';
import '../widgets/login_form.dart';
import '../widgets/logo_hr_auto.dart';
import '../widgets/painel_marca.dart';

/// Qual formulário o card está mostrando.
enum ModoAuth { login, cadastro }

/// Tela de autenticação: um único card que alterna entre Login e Cadastro
/// com transição (fade + deslize + ajuste de altura), sem trocar de rota.
///
/// Layout responsivo (mobile-first):
///  - largura < 960 px: faixa em gradiente no topo, logo e card centralizado (até 460 px);
///  - largura >= 960 px: painel da marca à esquerda e card centralizado à direita.
class AuthPage extends StatefulWidget {
  const AuthPage({super.key, this.modoInicial = ModoAuth.login});

  final ModoAuth modoInicial;

  @override
  State<AuthPage> createState() => _AuthPageState();
}

class _AuthPageState extends State<AuthPage> {
  static const double _larguraDesktop = 960;
  static const double _larguraMaximaCard = 460;

  late ModoAuth _modo = widget.modoInicial;
  String? _emailRecemCadastrado;

  void _irPara(ModoAuth modo) => setState(() => _modo = modo);

  void _aoCadastrar(String email) {
    setState(() {
      _emailRecemCadastrado = email;
      _modo = ModoAuth.login;
    });
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(content: Text('Conta criada! Entre com seu e-mail e senha.')),
      );
  }

  /// Card com os formulários e a animação de troca.
  Widget _formularios() {
    return AnimatedSize(
      duration: const Duration(milliseconds: 320),
      curve: Curves.easeOutCubic,
      alignment: Alignment.topCenter,
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 320),
        switchInCurve: Curves.easeOutCubic,
        switchOutCurve: Curves.easeInCubic,
        // Mantém os dois formulários alinhados ao topo durante a troca.
        layoutBuilder: (atual, anteriores) => Stack(
          alignment: Alignment.topCenter,
          children: [...anteriores, if (atual != null) atual],
        ),
        transitionBuilder: (child, animacao) => FadeTransition(
          opacity: animacao,
          child: SlideTransition(
            position: Tween<Offset>(begin: const Offset(0, 0.03), end: Offset.zero).animate(animacao),
            child: child,
          ),
        ),
        child: switch (_modo) {
          ModoAuth.login => LoginForm(
              key: const ValueKey(ModoAuth.login),
              emailInicial: _emailRecemCadastrado,
              onIrParaCadastro: () => _irPara(ModoAuth.cadastro),
            ),
          ModoAuth.cadastro => CadastroForm(
              key: const ValueKey(ModoAuth.cadastro),
              onIrParaLogin: () => _irPara(ModoAuth.login),
              onCadastrado: _aoCadastrar,
            ),
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: LayoutBuilder(
        builder: (context, limites) => limites.maxWidth >= _larguraDesktop
            ? _layoutDesktop()
            : _layoutMobile(limites),
      ),
    );
  }

  Widget _layoutDesktop() {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Expanded(flex: 5, child: PainelMarca()),
        Expanded(
          flex: 6,
          child: SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(horizontal: 48, vertical: 40),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: _larguraMaximaCard),
                  child: CartaoFormulario(child: _formularios()),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _layoutMobile(BoxConstraints limites) {
    final topo = MediaQuery.paddingOf(context).top; // barra de status do celular
    final compacto = limites.maxWidth < 420;

    return SingleChildScrollView(
      child: ConstrainedBox(
        constraints: BoxConstraints(minWidth: limites.maxWidth, minHeight: limites.maxHeight),
        child: Stack(
          children: [
            // Faixa em gradiente atrás do logo; o card "flutua" sobre a borda inferior dela.
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: 250 + topo,
              child: const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [AppColors.primaria, AppColors.secundaria],
                  ),
                  borderRadius: BorderRadius.vertical(bottom: Radius.circular(32)),
                ),
              ),
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(16, topo + 36, 16, 32),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: _larguraMaximaCard),
                  child: Column(
                    children: [
                      const LogoHrAuto(claro: true, tamanho: 44),
                      const SizedBox(height: 10),
                      Text(
                        'Ponto, pessoas e compliance num só lugar.',
                        textAlign: TextAlign.center,
                        style: Theme.of(context)
                            .textTheme
                            .bodyMedium
                            ?.copyWith(color: AppColors.brancoTransl80),
                      ),
                      const SizedBox(height: 28),
                      CartaoFormulario(compacto: compacto, child: _formularios()),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
