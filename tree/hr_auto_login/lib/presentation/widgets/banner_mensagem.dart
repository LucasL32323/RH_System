import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

enum TipoMensagem { erro, sucesso, info }

/// Faixa de mensagem dentro do card (erro da API, sucesso, dica do modo protótipo).
/// É anunciada pelo leitor de tela (liveRegion) assim que aparece.
class BannerMensagem extends StatelessWidget {
  const BannerMensagem({super.key, required this.mensagem, this.tipo = TipoMensagem.erro});

  final String mensagem;
  final TipoMensagem tipo;

  @override
  Widget build(BuildContext context) {
    final (fundo, cor, icone) = switch (tipo) {
      TipoMensagem.erro => (AppColors.erroFundo, AppColors.erro, Icons.error_outline_rounded),
      TipoMensagem.sucesso => (AppColors.sucessoFundo, AppColors.sucesso, Icons.check_circle_outline_rounded),
      TipoMensagem.info => (AppColors.infoFundo, AppColors.primaria, Icons.info_outline_rounded),
    };

    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: fundo,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icone, color: cor, size: 20),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                mensagem,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: cor, height: 1.35),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
