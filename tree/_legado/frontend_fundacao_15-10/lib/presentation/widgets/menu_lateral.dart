import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

/// Item do menu lateral: somente ícone, com tooltip (RNF10).
class ItemMenu {
  const ItemMenu({required this.icone, required this.rotulo});

  final IconData icone;
  final String rotulo;
}

/// Menu vertical lateral só com ícones, igual no celular e no desktop.
/// Uma aba por vez: o item selecionado fica destacado.
class MenuLateral extends StatelessWidget {
  const MenuLateral({
    super.key,
    required this.itens,
    required this.indiceSelecionado,
    required this.aoSelecionar,
  });

  final List<ItemMenu> itens;
  final int indiceSelecionado;
  final ValueChanged<int> aoSelecionar;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 64,
      color: AppColors.principal,
      child: SafeArea(
        child: Column(
          children: [
            const SizedBox(height: 16),
            for (var i = 0; i < itens.length; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Tooltip(
                  message: itens[i].rotulo,
                  child: IconButton(
                    key: ValueKey('menu_item_$i'),
                    onPressed: () => aoSelecionar(i),
                    icon: Icon(itens[i].icone),
                    color: i == indiceSelecionado ? Colors.white : AppColors.apoio,
                    style: IconButton.styleFrom(
                      backgroundColor: i == indiceSelecionado
                          ? AppColors.destaque
                          : Colors.transparent,
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
