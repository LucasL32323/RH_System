import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../admin/componentes_admin.dart';
import '../admin/menu_icones_admin.dart' show iniciaisDoUsuario;
import 'abas_perfil.dart';

/// Barra superior (64 px) das áreas Colaborador e RH: trilha "Área › aba", data e usuário.
/// Não mostra o texto exato do perfil ("Colaborador"/"RH"): quem mostra a etiqueta é o Início.
class BarraSuperiorPerfil extends StatelessWidget {
  const BarraSuperiorPerfil({super.key, required this.usuario, required this.aba, required this.hoje});

  final UsuarioAutenticado usuario;
  final AbaPerfil aba;
  final DateTime hoje;

  String get _area => usuario.perfil == 'RH' ? 'Área do RH' : 'Área do colaborador';

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
                  Text(_area, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave)),
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
          if (completa) ...[
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Text(
                Formatadores.dataPorExtenso(hoje),
                style: const TextStyle(fontSize: 14, color: AppColors.textoSuave),
              ),
            ),
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
