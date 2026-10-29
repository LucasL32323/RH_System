import 'package:flutter/material.dart';

/// Botão de ação principal do formulário. Durante o envio mostra um spinner
/// e fica desabilitado (evita enviar duas vezes).
class BotaoPrincipal extends StatelessWidget {
  const BotaoPrincipal({
    super.key,
    required this.texto,
    required this.onPressed,
    this.carregando = false,
  });

  final String texto;
  final VoidCallback? onPressed;
  final bool carregando;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: carregando ? null : onPressed,
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 200),
        child: carregando
            ? const SizedBox(
                key: ValueKey('carregando'),
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2.4, color: Colors.white),
              )
            : Text(texto, key: const ValueKey('texto')),
      ),
    );
  }
}
