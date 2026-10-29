import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/validation/validadores.dart';

/// Barra de força + checklist da política de senha, atualizados enquanto o usuário digita.
/// As regras são as mesmas da PoliticaSenha do back-end.
class RequisitosSenhaView extends StatelessWidget {
  const RequisitosSenhaView({super.key, required this.senha});

  final String senha;

  @override
  Widget build(BuildContext context) {
    final r = RequisitosSenha.avaliar(senha);
    final pontuacao = senha.isEmpty ? 0 : r.pontuacao;

    final (corBarra, rotulo) = switch (pontuacao) {
      0 => (AppColors.borda, ''),
      <= 2 => (AppColors.erro, 'Fraca'),
      <= 4 => (const Color(0xFFB7791F), 'Média'),
      _ => (AppColors.sucesso, 'Forte'),
    };

    final itens = <(bool, String)>[
      (r.tamanho, '8+ caracteres'),
      (r.maiuscula, 'Maiúscula'),
      (r.minuscula, 'Minúscula'),
      (r.numero, 'Número'),
      (r.simbolo, 'Símbolo'),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: TweenAnimationBuilder<double>(
                  tween: Tween(end: pontuacao / 5),
                  duration: const Duration(milliseconds: 250),
                  builder: (_, valor, __) => LinearProgressIndicator(
                    value: valor,
                    minHeight: 6,
                    backgroundColor: AppColors.borda,
                    color: corBarra,
                  ),
                ),
              ),
            ),
            if (rotulo.isNotEmpty) ...[
              const SizedBox(width: 10),
              Text(
                rotulo,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: corBarra,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ],
          ],
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 12,
          runSpacing: 6,
          children: [
            for (final (ok, texto) in itens) _Requisito(ok: ok, texto: texto),
          ],
        ),
      ],
    );
  }
}

class _Requisito extends StatelessWidget {
  const _Requisito({required this.ok, required this.texto});

  final bool ok;
  final String texto;

  @override
  Widget build(BuildContext context) {
    final cor = ok ? AppColors.sucesso : AppColors.textoSuave;
    return Semantics(
      label: '$texto: ${ok ? 'atendido' : 'pendente'}',
      excludeSemantics: true,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          AnimatedSwitcher(
            duration: const Duration(milliseconds: 200),
            child: Icon(
              ok ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded,
              key: ValueKey(ok),
              size: 16,
              color: cor,
            ),
          ),
          const SizedBox(width: 4),
          Text(texto, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: cor)),
        ],
      ),
    );
  }
}
