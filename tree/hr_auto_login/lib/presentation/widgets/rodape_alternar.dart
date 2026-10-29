import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

/// "Não tem uma conta? Cadastre-se" / "Já tem uma conta? Entrar".
class RodapeAlternar extends StatelessWidget {
  const RodapeAlternar({super.key, required this.pergunta, required this.acao, required this.onPressed});

  final String pergunta;
  final String acao;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      alignment: WrapAlignment.center,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          pergunta,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textoSuave),
        ),
        TextButton(onPressed: onPressed, child: Text(acao)),
      ],
    );
  }
}
