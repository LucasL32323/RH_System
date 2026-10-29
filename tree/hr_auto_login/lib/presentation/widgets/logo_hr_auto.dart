import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

/// Marca do HR-Auto: ícone em bloco arredondado + nome.
/// [claro] = versão para fundos escuros (painel da marca e faixa do celular).
class LogoHrAuto extends StatelessWidget {
  const LogoHrAuto({super.key, this.claro = false, this.tamanho = 40});

  final bool claro;
  final double tamanho;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'HR-Auto',
      excludeSemantics: true,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: tamanho,
            height: tamanho,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(tamanho * 0.3),
              color: claro ? AppColors.brancoTransl20 : null,
              gradient: claro
                  ? null
                  : const LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [AppColors.destaque, AppColors.primaria],
                    ),
              border: claro ? Border.all(color: AppColors.brancoTransl20) : null,
            ),
            child: Icon(Icons.badge_rounded, color: Colors.white, size: tamanho * 0.55),
          ),
          SizedBox(width: tamanho * 0.3),
          Text(
            'HR-Auto',
            style: TextStyle(
              fontSize: tamanho * 0.55,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.4,
              color: claro ? Colors.white : AppColors.texto,
            ),
          ),
        ],
      ),
    );
  }
}
