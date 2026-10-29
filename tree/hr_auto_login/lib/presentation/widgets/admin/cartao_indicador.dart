import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import 'componentes_admin.dart';

/// Card de KPI: rótulo + ícone, valor grande e uma linha de contexto (variação + nota).
class CartaoIndicador extends StatelessWidget {
  const CartaoIndicador({
    super.key,
    required this.rotulo,
    required this.valor,
    required this.icone,
    required this.variacao,
    required this.iconeVariacao,
    required this.tom,
    required this.nota,
  });

  final String rotulo;
  final String valor;
  final IconData icone;
  final String variacao;
  final IconData iconeVariacao;
  final TomStatus tom;
  final String nota;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  rotulo,
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.textoSuave),
                ),
              ),
              const SizedBox(width: 12),
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.infoFundo,
                  borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                ),
                child: Icon(icone, size: 20, color: AppColors.primaria),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            valor,
            style: const TextStyle(
              fontSize: 28,
              height: 1,
              fontWeight: FontWeight.w700,
              letterSpacing: -0.6,
              color: AppColors.texto,
              fontFeatures: numerosTabulares,
            ),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            runSpacing: 6,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(color: tom.fundo, borderRadius: BorderRadius.circular(999)),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(iconeVariacao, size: 14, color: tom.texto),
                    const SizedBox(width: 4),
                    Text(
                      variacao,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: tom.texto,
                        fontFeatures: numerosTabulares,
                      ),
                    ),
                  ],
                ),
              ),
              Text(nota, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
            ],
          ),
        ],
      ),
    );
  }
}
