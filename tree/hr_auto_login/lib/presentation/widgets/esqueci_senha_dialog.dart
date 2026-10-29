import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/validation/validadores.dart';
import '../viewmodels/recuperar_senha_view_model.dart';
import 'banner_mensagem.dart';
import 'botao_principal.dart';
import 'campo_texto.dart';

/// Diálogo "Esqueci minha senha" (RF de recuperação de senha).
/// Envia o e-mail para POST /api/v1/auth/esqueci-senha; a resposta é sempre a mesma,
/// exista ou não a conta, para não revelar quem está cadastrado.
class EsqueciSenhaDialog extends ConsumerStatefulWidget {
  const EsqueciSenhaDialog({super.key, this.emailInicial});

  final String? emailInicial;

  @override
  ConsumerState<EsqueciSenhaDialog> createState() => _EsqueciSenhaDialogState();
}

class _EsqueciSenhaDialogState extends ConsumerState<EsqueciSenhaDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _email;

  @override
  void initState() {
    super.initState();
    _email = TextEditingController(text: widget.emailInicial ?? '');
  }

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    await ref.read(recuperarSenhaViewModelProvider.notifier).solicitar(_email.text);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(recuperarSenhaViewModelProvider);
    final texto = Theme.of(context).textTheme;
    final enviado = estado.sucesso != null;

    return Dialog(
      backgroundColor: AppColors.superficie,
      surfaceTintColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppTheme.raioCard)),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(28),
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: AppColors.infoFundo,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Icon(Icons.key_rounded, color: AppColors.primaria),
                    ),
                    const Spacer(),
                    IconButton(
                      tooltip: 'Fechar',
                      onPressed: () => Navigator.of(context).pop(),
                      icon: const Icon(Icons.close_rounded),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Text('Recuperar senha', style: texto.titleLarge?.copyWith(fontWeight: FontWeight.w800)),
                const SizedBox(height: 6),
                Text(
                  'Informe seu e-mail. Se ele estiver cadastrado, enviaremos um link '
                  'válido por 30 minutos para criar uma nova senha.',
                  style: texto.bodyMedium?.copyWith(color: AppColors.textoSuave, height: 1.4),
                ),
                const SizedBox(height: 22),
                if (estado.erro != null) ...[
                  BannerMensagem(mensagem: estado.erro!),
                  const SizedBox(height: 16),
                ],
                if (enviado) ...[
                  BannerMensagem(tipo: TipoMensagem.sucesso, mensagem: estado.sucesso!),
                  const SizedBox(height: 22),
                  FilledButton(
                    onPressed: () => Navigator.of(context).pop(),
                    child: const Text('Voltar ao login'),
                  ),
                ] else ...[
                  CampoTexto(
                    rotulo: 'E-mail',
                    controller: _email,
                    dica: 'nome@empresa.com.br',
                    icone: Icons.mail_outline_rounded,
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.send,
                    autofillHints: const [AutofillHints.email],
                    validator: Validadores.email,
                    enabled: !estado.carregando,
                    autofocus: widget.emailInicial == null,
                    onSubmitted: (_) => _enviar(),
                  ),
                  const SizedBox(height: 22),
                  BotaoPrincipal(
                    texto: 'Enviar link',
                    carregando: estado.carregando,
                    onPressed: _enviar,
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
