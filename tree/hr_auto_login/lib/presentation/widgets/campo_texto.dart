import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

/// Campo de formulário com rótulo fixo acima (mais legível que o rótulo flutuante
/// e igual ao protótipo do Figma). Usado por todos os formulários de autenticação.
class CampoTexto extends StatelessWidget {
  const CampoTexto({
    super.key,
    required this.rotulo,
    required this.controller,
    this.dica,
    this.icone,
    this.validator,
    this.keyboardType,
    this.textInputAction = TextInputAction.next,
    this.autofillHints,
    this.textCapitalization = TextCapitalization.none,
    this.obscureText = false,
    this.sufixo,
    this.onChanged,
    this.onSubmitted,
    this.enabled = true,
    this.autofocus = false,
  });

  final String rotulo;
  final TextEditingController controller;
  final String? dica;
  final IconData? icone;
  final FormFieldValidator<String>? validator;
  final TextInputType? keyboardType;
  final TextInputAction textInputAction;
  final Iterable<String>? autofillHints;
  final TextCapitalization textCapitalization;
  final bool obscureText;
  final Widget? sufixo;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;
  final bool autofocus;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          rotulo,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
                fontWeight: FontWeight.w600,
                color: AppColors.texto,
              ),
        ),
        const SizedBox(height: 8),
        TextFormField(
          key: Key('campo_$rotulo'), // usado pelos testes de widget
          controller: controller,
          validator: validator,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          autofillHints: autofillHints,
          textCapitalization: textCapitalization,
          obscureText: obscureText,
          enableSuggestions: !obscureText,
          autocorrect: false,
          enabled: enabled,
          autofocus: autofocus,
          onChanged: onChanged,
          onFieldSubmitted: onSubmitted,
          decoration: InputDecoration(
            hintText: dica,
            prefixIcon: icone == null ? null : Icon(icone, size: 20),
            suffixIcon: sufixo,
          ),
        ),
      ],
    );
  }
}

/// Campo de senha com botão de mostrar/ocultar.
class CampoSenha extends StatefulWidget {
  const CampoSenha({
    super.key,
    required this.rotulo,
    required this.controller,
    this.dica = '••••••••',
    this.validator,
    this.textInputAction = TextInputAction.next,
    this.autofillHints,
    this.onChanged,
    this.onSubmitted,
    this.enabled = true,
  });

  final String rotulo;
  final TextEditingController controller;
  final String dica;
  final FormFieldValidator<String>? validator;
  final TextInputAction textInputAction;
  final Iterable<String>? autofillHints;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;

  @override
  State<CampoSenha> createState() => _CampoSenhaState();
}

class _CampoSenhaState extends State<CampoSenha> {
  bool _oculta = true;

  @override
  Widget build(BuildContext context) {
    return CampoTexto(
      rotulo: widget.rotulo,
      controller: widget.controller,
      dica: widget.dica,
      icone: Icons.lock_outline_rounded,
      validator: widget.validator,
      keyboardType: TextInputType.visiblePassword,
      textInputAction: widget.textInputAction,
      autofillHints: widget.autofillHints,
      obscureText: _oculta,
      onChanged: widget.onChanged,
      onSubmitted: widget.onSubmitted,
      enabled: widget.enabled,
      sufixo: IconButton(
        tooltip: _oculta ? 'Mostrar senha' : 'Ocultar senha',
        icon: Icon(_oculta ? Icons.visibility_outlined : Icons.visibility_off_outlined, size: 20),
        onPressed: () => setState(() => _oculta = !_oculta),
      ),
    );
  }
}
