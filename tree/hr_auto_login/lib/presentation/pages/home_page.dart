import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../domain/entities/usuario_autenticado.dart';
import '../providers.dart';
import '../widgets/cartao_formulario.dart';
import '../widgets/logo_hr_auto.dart';

/// Tela provisória pós-login: confirma que a autenticação funcionou.
/// Será substituída pelo dashboard por perfil (Colaborador / RH / Admin) na próxima fase.
class HomePage extends ConsumerWidget {
  const HomePage({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final texto = Theme.of(context).textTheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: Column(
                children: [
                  const LogoHrAuto(),
                  const SizedBox(height: 28),
                  CartaoFormulario(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircleAvatar(
                          radius: 32,
                          backgroundColor: AppColors.infoFundo,
                          child: Text(
                            usuario.primeiroNome.isEmpty
                                ? '?'
                                : usuario.primeiroNome.characters.first.toUpperCase(),
                            style: texto.headlineSmall?.copyWith(
                              color: AppColors.primaria,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                        Text(
                          'Olá, ${usuario.primeiroNome}!',
                          textAlign: TextAlign.center,
                          style: texto.headlineSmall?.copyWith(fontWeight: FontWeight.w800),
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          alignment: WrapAlignment.center,
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            _Etiqueta(texto: usuario.codigo),
                            _Etiqueta(texto: usuario.perfil, destaque: true),
                          ],
                        ),
                        const SizedBox(height: 20),
                        Text(
                          'Login validado. O dashboard do seu perfil entra na próxima etapa do projeto.',
                          textAlign: TextAlign.center,
                          style: texto.bodyMedium?.copyWith(color: AppColors.textoSuave, height: 1.4),
                        ),
                        const SizedBox(height: 28),
                        OutlinedButton.icon(
                          onPressed: () => ref.read(sessaoProvider.notifier).state = null,
                          icon: const Icon(Icons.logout_rounded, size: 18),
                          label: const Text('Sair'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Etiqueta arredondada (código FUNC-0001 e perfil de acesso).
class _Etiqueta extends StatelessWidget {
  const _Etiqueta({required this.texto, this.destaque = false});

  final String texto;
  final bool destaque;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: destaque ? AppColors.primaria : AppColors.infoFundo,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        texto,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: destaque ? Colors.white : AppColors.primaria,
              fontWeight: FontWeight.w700,
            ),
      ),
    );
  }
}
