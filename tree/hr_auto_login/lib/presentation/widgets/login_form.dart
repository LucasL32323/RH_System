import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/config/app_config.dart';
import '../../core/theme/app_colors.dart';
import '../../core/validation/validadores.dart';
import '../viewmodels/login_view_model.dart';
import 'banner_mensagem.dart';
import 'botao_principal.dart';
import 'campo_texto.dart';
import 'esqueci_senha_dialog.dart';
import 'rodape_alternar.dart';

/// Formulário de login: e-mail/usuário, senha, "Esqueci minha senha", "Entrar"
/// e o link para alternar para o cadastro.
class LoginForm extends ConsumerStatefulWidget {
  const LoginForm({
    super.key,
    required this.onIrParaCadastro,
    this.emailInicial,
  });

  /// Troca o card para o formulário de cadastro (sem trocar de rota).
  final VoidCallback onIrParaCadastro;

  /// Preenche o e-mail (ex.: logo depois de criar a conta).
  final String? emailInicial;

  @override
  ConsumerState<LoginForm> createState() => _LoginFormState();
}

class _LoginFormState extends ConsumerState<LoginForm> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _login;
  final _senha = TextEditingController();
  bool _tentouEnviar = false; // só valida "ao digitar" depois da primeira tentativa

  @override
  void initState() {
    super.initState();
    _login = TextEditingController(text: widget.emailInicial ?? '');
  }

  @override
  void dispose() {
    _login.dispose();
    _senha.dispose();
    super.dispose();
  }

  Future<void> _entrar() async {
    FocusScope.of(context).unfocus();
    setState(() => _tentouEnviar = true);
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final ok = await ref.read(loginViewModelProvider.notifier).entrar(login: _login.text, senha: _senha.text);
    // Em caso de erro, limpa só a senha (padrão de segurança e de usabilidade).
    if (!ok && mounted) _senha.clear();
  }

  void _abrirEsqueciSenha() {
    final texto = _login.text.trim();
    showDialog<void>(
      context: context,
      builder: (_) => EsqueciSenhaDialog(emailInicial: Validadores.pareceEmail(texto) ? texto : null),
    );
  }

  void _irParaCadastro() {
    ref.read(loginViewModelProvider.notifier).limparErro();
    widget.onIrParaCadastro();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(loginViewModelProvider);
    final texto = Theme.of(context).textTheme;

    return AutofillGroup(
      child: Form(
        key: _formKey,
        autovalidateMode: _tentouEnviar ? AutovalidateMode.onUserInteraction : AutovalidateMode.disabled,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Bem-vindo de volta',
              style: texto.headlineSmall?.copyWith(fontWeight: FontWeight.w800, letterSpacing: -0.5),
            ),
            const SizedBox(height: 6),
            Text(
              'Entre com sua conta corporativa para continuar.',
              style: texto.bodyMedium?.copyWith(color: AppColors.textoSuave),
            ),
            const SizedBox(height: 28),
            if (estado.erro != null) ...[
              BannerMensagem(mensagem: estado.erro!),
              const SizedBox(height: 18),
            ],
            CampoTexto(
              rotulo: 'E-mail ou usuário',
              controller: _login,
              dica: 'nome@empresa.com.br ou FUNC-0001',
              icone: Icons.alternate_email_rounded,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.username, AutofillHints.email],
              validator: Validadores.loginUsuario,
              enabled: !estado.carregando,
              onChanged: (_) => ref.read(loginViewModelProvider.notifier).limparErro(),
            ),
            const SizedBox(height: 18),
            CampoSenha(
              rotulo: 'Senha',
              controller: _senha,
              textInputAction: TextInputAction.done,
              autofillHints: const [AutofillHints.password],
              validator: Validadores.senhaLogin,
              enabled: !estado.carregando,
              onSubmitted: (_) => _entrar(),
            ),
            const SizedBox(height: 6),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(
                onPressed: estado.carregando ? null : _abrirEsqueciSenha,
                child: const Text('Esqueci minha senha'),
              ),
            ),
            const SizedBox(height: 14),
            BotaoPrincipal(texto: 'Entrar', carregando: estado.carregando, onPressed: _entrar),
            if (AppConfig.modoPrototipo) ...[
              const SizedBox(height: 16),
              const BannerMensagem(
                tipo: TipoMensagem.info,
                mensagem: 'Modo protótipo: entre com ${AppConfig.emailDemo} / ${AppConfig.senhaDemo} '
                    'ou crie uma conta (fica só na memória).',
              ),
            ],
            const SizedBox(height: 24),
            RodapeAlternar(
              pergunta: 'Não tem uma conta?',
              acao: 'Cadastre-se',
              onPressed: estado.carregando ? null : _irParaCadastro,
            ),
          ],
        ),
      ),
    );
  }
}
