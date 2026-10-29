import 'package:flutter/material.dart';

import 'pagina_admin.dart';

/// Abre o diálogo do motivo da recusa de um ajuste de ponto. Devolve o texto (sem espaços nas pontas)
/// ou null se o gestor cancelar.
Future<String?> pedirMotivoRecusa(BuildContext context, {required String colaborador}) {
  return showDialog<String>(
    context: context,
    builder: (_) => DialogoMotivoRecusa(colaborador: colaborador),
  );
}

/// Pede o motivo ao recusar um pedido de ajuste. A API exige no mínimo 5 caracteres e guarda o texto
/// na observação do pedido, que o colaborador lê em "Minhas solicitações de ajuste".
class DialogoMotivoRecusa extends StatefulWidget {
  const DialogoMotivoRecusa({super.key, required this.colaborador});

  final String colaborador;

  static const int motivoMinimo = 5;
  static const int motivoMaximo = 500;

  @override
  State<DialogoMotivoRecusa> createState() => _DialogoMotivoRecusaState();
}

class _DialogoMotivoRecusaState extends State<DialogoMotivoRecusa> {
  final _form = GlobalKey<FormState>();
  final _motivo = TextEditingController();

  @override
  void dispose() {
    _motivo.dispose();
    super.dispose();
  }

  String? _validar(String? valor) {
    final texto = valor?.trim() ?? '';
    if (texto.isEmpty) return 'Informe o motivo da recusa.';
    if (texto.length < DialogoMotivoRecusa.motivoMinimo) {
      return 'Use pelo menos ${DialogoMotivoRecusa.motivoMinimo} caracteres.';
    }
    if (texto.length > DialogoMotivoRecusa.motivoMaximo) {
      return 'Use no máximo ${DialogoMotivoRecusa.motivoMaximo} caracteres.';
    }
    return null;
  }

  void _confirmar() {
    if (!_form.currentState!.validate()) return;
    Navigator.of(context).pop(_motivo.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    return DialogoAdmin(
      titulo: 'Recusar ajuste de ponto',
      subtitulo: 'O motivo fica registrado no pedido de ${widget.colaborador}, que pode lê-lo em '
          '"Minhas solicitações de ajuste". A decisão não pode ser desfeita.',
      rotuloConfirmar: 'Recusar pedido',
      onConfirmar: _confirmar,
      conteudo: Form(
        key: _form,
        child: CampoAdmin(
          rotulo: 'Motivo da recusa',
          controller: _motivo,
          dica: 'Explique por que o ajuste não foi aceito',
          maxLines: 3,
          textCapitalization: TextCapitalization.sentences,
          validator: _validar,
        ),
      ),
    );
  }
}
