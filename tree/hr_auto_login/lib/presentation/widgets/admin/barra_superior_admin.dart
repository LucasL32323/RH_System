import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import 'componentes_admin.dart';
import 'menu_icones_admin.dart';

/// Barra superior do Admin (64 px): trilha "Administração › aba", data, alertas e usuário.
class BarraSuperiorAdmin extends StatelessWidget {
  const BarraSuperiorAdmin({
    super.key,
    required this.usuario,
    required this.aba,
    required this.hoje,
    required this.alertas,
    required this.onAlertas,
  });

  final UsuarioAutenticado usuario;
  final AbaAdmin aba;
  final DateTime hoje;
  final int alertas;
  final VoidCallback onAlertas;

  @override
  Widget build(BuildContext context) {
    final largura = MediaQuery.sizeOf(context).width;
    final completa = largura >= 760;

    return Container(
      height: 64,
      padding: EdgeInsets.symmetric(horizontal: completa ? 32 : 16),
      decoration: const BoxDecoration(
        color: AppColors.fundo,
        border: Border(bottom: BorderSide(color: AppColors.borda)),
      ),
      child: Row(
        children: [
          Expanded(
            child: Row(
              children: [
                if (completa) ...[
                  const Text('Administração', style: TextStyle(fontSize: 14, color: AppColors.textoSuave)),
                  const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 6),
                    child: Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.textoSuave),
                  ),
                ],
                Flexible(
                  child: Text(
                    aba.rotulo,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                  ),
                ),
              ],
            ),
          ),
          if (completa)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Text(
                Formatadores.dataPorExtenso(hoje),
                style: const TextStyle(fontSize: 14, color: AppColors.textoSuave),
              ),
            ),
          IconButton(
            onPressed: onAlertas,
            tooltip: alertas == 0 ? 'Alertas' : 'Alertas ($alertas)',
            icon: Badge(
              isLabelVisible: alertas > 0,
              label: Text('$alertas'),
              backgroundColor: AppColors.erro,
              child: const Icon(Icons.notifications_none_rounded, color: AppColors.textoSuave),
            ),
          ),
          if (completa) ...[
            Container(width: 1, height: 24, margin: const EdgeInsets.symmetric(horizontal: 8), color: AppColors.borda),
            AvatarIniciais(
              iniciais: iniciaisDoUsuario(usuario.nome),
              tamanho: 32,
              fundo: AppColors.primaria,
              cor: Colors.white,
            ),
            const SizedBox(width: 8),
            Text(
              usuario.primeiroNome,
              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto),
            ),
          ],
        ],
      ),
    );
  }
}
