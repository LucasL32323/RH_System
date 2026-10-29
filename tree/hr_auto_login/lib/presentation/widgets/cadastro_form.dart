import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../core/validation/validadores.dart';
import '../viewmodels/cadastro_view_model.dart';
import 'banner_mensagem.dart';
import 'botao_principal.dart';
import 'campo_texto.dart';
import 'requisitos_senha_view.dart';
import 'rodape_alternar.dart';

/// Formulário de cadastro: nome completo, e-mail, senha (com checklist da política),
/// confirmação e "Criar conta". Em caso de sucesso devolve o e-mail para o login.
class CadastroForm extends ConsumerStatefulWidget {
  const CadastroForm({
    super.key,
    required this.onIrParaLogin,
    required this.onCadastrado,
  });

  final VoidCallback onIrParaLogin;

  /// Chamado com o e-mail da conta criada (o login abre com ele preenchido).
  final ValueChanged<String> onCadastrado;

  @override
  ConsumerState<CadastroForm> createState() => _CadastroFormState();
}

class _CadastroFormState extends ConsumerState<CadastroForm> {
  final _formKey = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _email = TextEditingController();
  final _senha = TextEditingController();
  final _confirmacao = TextEditingController();
  bool _tentouEnviar = false;

  @override
  void dispose() {
    _nome.dispose();
    _email.dispose();
    _senha.dispose();
    _confirmacao.dispose();
    super.dispose();
  }

  Future<void> _criarConta() async {
    FocusScope.of(context).unfocus();
    setState(() => _tentouEnviar = true);
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final email = _email.text.trim().toLowerCase();
    final ok = await ref.read(cadastroViewModelProvider.notifier).cadastrar(
          nome: _nome.text,
          email: email,
          senha: _senha.text,
        );
    if (ok && mounted) widget.onCadastrado(email);
  }

  void _irParaLogin() {
    ref.read(cadastroViewModelProvider.notifier).limparErro();
    widget.onIrParaLogin();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(cadastroViewModelProvider);
    final texto = Theme.of(context).textTheme;
    final habilitado = !estado.carregando;

    return AutofillGroup(
      child: Form(
        key: _formKey,
        autovalidateMode: _tentouEnviar ? AutovalidateMode.onUserInteraction : AutovalidateMode.disabled,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Crie sua conta',
              style: texto.headlineSmall?.copyWith(fontWeight: FontWeight.w800, letterSpacing: -0.5),
            ),
            const SizedBox(height: 6),
            Text(
              'Leva menos de um minuto. Use seu e-mail corporativo.',
              style: texto.bodyMedium?.copyWith(color: AppColors.textoSuave),
            ),
            const SizedBox(height: 28),
            if (estado.erro != null) ...[
              BannerMensagem(mensagem: estado.erro!),
              const SizedBox(height: 18),
            ],
            CampoTexto(
              rotulo: 'Nome completo',
              controller: _nome,
              dica: 'Ex.: Ana Paula Souza',
              icone: Icons.person_outline_rounded,
              keyboardType: TextInputType.name,
              textCapitalization: TextCapitalization.words,
              autofillHints: const [AutofillHints.name],
              validator: Validadores.nomeCompleto,
              enabled: habilitado,
              onChanged: (_) => ref.read(cadastroViewModelProvider.notifier).limparErro(),
            ),
            const SizedBox(height: 18),
            CampoTexto(
              rotulo: 'E-mail',
              controller: _email,
              dica: 'nome@empresa.com.br',
              icone: Icons.mail_outline_rounded,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              validator: Validadores.email,
              enabled: habilitado,
              onChanged: (_) => ref.read(cadastroViewModelProvider.notifier).limparErro(),
            ),
            const SizedBox(height: 18),
            CampoSenha(
              rotulo: 'Senha',
              controller: _senha,
              autofillHints: const [AutofillHints.newPassword],
              validator: Validadores.senhaNova,
              enabled: habilitado,
              // Redesenha o checklist e revalida a confirmação enquanto digita.
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 12),
            RequisitosSenhaView(senha: _senha.text),
            const SizedBox(height: 18),
            CampoSenha(
              rotulo: 'Confirmação de senha',
              controller: _confirmacao,
              textInputAction: TextInputAction.done,
              autofillHints: const [AutofillHints.newPassword],
              validator: (valor) => Validadores.confirmacaoSenha(valor, _senha.text),
              enabled: habilitado,
              onSubmitted: (_) => _criarConta(),
            ),
            const SizedBox(height: 28),
            BotaoPrincipal(texto: 'Criar conta', carregando: estado.carregando, onPressed: _criarConta),
            const SizedBox(height: 16),
            Text(
              'Ao criar a conta, você concorda com o uso dos seus dados para fins de gestão de RH, '
              'conforme a LGPD (Lei 13.709/2018).',
              textAlign: TextAlign.center,
              style: texto.bodySmall?.copyWith(color: AppColors.textoSuave, height: 1.4),
            ),
            const SizedBox(height: 16),
            RodapeAlternar(
              pergunta: 'Já tem uma conta?',
              acao: 'Entrar',
              onPressed: habilitado ? _irParaLogin : null,
            ),
          ],
        ),
      ),
    );
  }
}
