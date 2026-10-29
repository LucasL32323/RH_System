import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';

/// Card branco do formulário: cantos de 20 px, borda fina e sombra leve em dois níveis
/// (uma difusa, tingida de petróleo, e uma curta para "assentar" o card no fundo).
class CartaoFormulario extends StatelessWidget {
  const CartaoFormulario({super.key, required this.child, this.compacto = false});

  final Widget child;

  /// true em telas estreitas: menos padding interno.
  final bool compacto;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: compacto ? 22 : 36, vertical: compacto ? 28 : 36),
      decoration: BoxDecoration(
        color: AppColors.superficie,
        borderRadius: BorderRadius.circular(AppTheme.raioCard),
        border: Border.all(color: AppColors.borda),
        boxShadow: const [
          BoxShadow(color: AppColors.sombraCard, blurRadius: 40, offset: Offset(0, 16)),
          BoxShadow(color: AppColors.sombraFina, blurRadius: 4, offset: Offset(0, 1)),
        ],
      ),
      child: child,
    );
  }
}
