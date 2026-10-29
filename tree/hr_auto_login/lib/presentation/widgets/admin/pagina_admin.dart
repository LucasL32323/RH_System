import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import 'componentes_admin.dart';

/// Estrutura comum das abas do Admin: rolagem vertical, largura máxima de 1376 px,
/// título + subtítulo à esquerda e ações à direita (empilham abaixo de 900 px).
class PaginaAdmin extends StatelessWidget {
  const PaginaAdmin({
    super.key,
    required this.titulo,
    required this.subtitulo,
    required this.children,
    this.acoes = const [],
  });

  final String titulo;
  final String subtitulo;
  final List<Widget> acoes;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final margem = limites.maxWidth >= 900 ? 32.0 : 16.0;
        return SingleChildScrollView(
          padding: EdgeInsets.fromLTRB(margem, 28, margem, 48),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 1376),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _Cabecalho(titulo: titulo, subtitulo: subtitulo, acoes: acoes),
                  const SizedBox(height: 24),
                  ...children,
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _Cabecalho extends StatelessWidget {
  const _Cabecalho({required this.titulo, required this.subtitulo, required this.acoes});

  final String titulo;
  final String subtitulo;
  final List<Widget> acoes;

  @override
  Widget build(BuildContext context) {
    final textos = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Semantics(
          header: true,
          child: Text(
            titulo,
            style: const TextStyle(
              fontSize: 28,
              height: 1.2,
              fontWeight: FontWeight.w700,
              letterSpacing: -0.6,
              color: AppColors.texto,
            ),
          ),
        ),
        const SizedBox(height: 6),
        Text(subtitulo, style: const TextStyle(fontSize: 14, height: 1.4, color: AppColors.textoSuave)),
      ],
    );
    if (acoes.isEmpty) return textos;
    final botoes = Wrap(spacing: 12, runSpacing: 12, children: acoes);

    return LayoutBuilder(
      builder: (context, limites) {
        if (limites.maxWidth < 900) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [textos, const SizedBox(height: 16), botoes],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [Expanded(child: textos), const SizedBox(width: 16), botoes],
        );
      },
    );
  }
}

/// Grade de cards com o mesmo tamanho por linha: [colunas] no desktop, 2 a partir de 520 px, 1 abaixo.
class GradeResponsiva extends StatelessWidget {
  const GradeResponsiva({super.key, required this.itens, this.colunas = 4, this.larguraParaTodas = 1040});

  final List<Widget> itens;
  final int colunas;

  /// Largura a partir da qual a grade usa [colunas].
  final double larguraParaTodas;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final n = limites.maxWidth >= larguraParaTodas
            ? colunas
            : limites.maxWidth >= 520
                ? math.min(2, colunas)
                : 1;
        final linhas = <Widget>[];
        for (var i = 0; i < itens.length; i += n) {
          if (i > 0) linhas.add(const SizedBox(height: 24));
          final daLinha = itens.sublist(i, math.min(i + n, itens.length));
          linhas.add(IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (var j = 0; j < n; j++) ...[
                  if (j > 0) const SizedBox(width: 24),
                  Expanded(child: j < daLinha.length ? daLinha[j] : const SizedBox.shrink()),
                ],
              ],
            ),
          ));
        }
        return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: linhas);
      },
    );
  }
}

/// Dois blocos lado a lado (proporção [flexEsquerda]:[flexDireita]) que empilham abaixo de [larguraMinima].
class DuasColunas extends StatelessWidget {
  const DuasColunas({
    super.key,
    required this.esquerda,
    required this.direita,
    this.flexEsquerda = 2,
    this.flexDireita = 1,
    this.larguraMinima = 1040,
  });

  final Widget esquerda;
  final Widget direita;
  final int flexEsquerda;
  final int flexDireita;
  final double larguraMinima;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        if (limites.maxWidth < larguraMinima) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [esquerda, const SizedBox(height: 24), direita],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(flex: flexEsquerda, child: esquerda),
            const SizedBox(width: 24),
            Expanded(flex: flexDireita, child: direita),
          ],
        );
      },
    );
  }
}

/// Barra horizontal de progresso (adesão, quadro, distribuição) com trilha suave.
class BarraProgresso extends StatelessWidget {
  const BarraProgresso({super.key, required this.fracao, this.cor = AppColors.primaria, this.altura = 8});

  final double fracao;
  final Color cor;
  final double altura;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(999),
      child: SizedBox(
        height: altura,
        child: Stack(
          fit: StackFit.expand,
          children: [
            const ColoredBox(color: AppColors.superficieSuave),
            FractionallySizedBox(
              alignment: Alignment.centerLeft,
              widthFactor: fracao.clamp(0, 1).toDouble(),
              child: ColoredBox(color: cor),
            ),
          ],
        ),
      ),
    );
  }
}

/// Carregando dentro de uma aba.
class EstadoCarregando extends StatelessWidget {
  const EstadoCarregando({super.key, this.texto = 'Carregando…'});

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 120),
      child: Column(
        children: [
          const CircularProgressIndicator(),
          const SizedBox(height: 16),
          Text(texto, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave)),
        ],
      ),
    );
  }
}

/// Erro de carregamento com "Tentar novamente".
class EstadoErro extends StatelessWidget {
  const EstadoErro({super.key, required this.titulo, required this.onTentar});

  final String titulo;
  final VoidCallback onTentar;

  @override
  Widget build(BuildContext context) {
    return AvisoCentral(
      icone: Icons.cloud_off_rounded,
      titulo: titulo,
      texto: 'Verifique a conexão com o servidor e tente novamente.',
      botao: BotaoAdmin(
        rotulo: 'Tentar novamente',
        icone: Icons.refresh_rounded,
        variante: VarianteBotao.primario,
        onPressed: onTentar,
      ),
    );
  }
}

class AvisoCentral extends StatelessWidget {
  const AvisoCentral({super.key, required this.icone, required this.titulo, required this.texto, this.botao});

  final IconData icone;
  final String titulo;
  final String texto;
  final Widget? botao;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: PainelAdmin(
          padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 48),
          child: Column(
            children: [
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  color: AppColors.infoFundo,
                  borderRadius: BorderRadius.circular(AppTheme.raioCard),
                ),
                child: Icon(icone, size: 26, color: AppColors.primaria),
              ),
              const SizedBox(height: 20),
              Text(
                titulo,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.texto),
              ),
              const SizedBox(height: 8),
              Text(
                texto,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 14, height: 1.5, color: AppColors.textoSuave),
              ),
              if (botao != null) ...[const SizedBox(height: 24), botao!],
            ],
          ),
        ),
      ),
    );
  }
}

/// Campo de formulário das abas do Admin: rótulo fixo acima, como o [CampoTexto] do login,
/// com máscara opcional. A chave 'campo_<rótulo>' é usada pelos testes.
class CampoAdmin extends StatelessWidget {
  const CampoAdmin({
    super.key,
    required this.rotulo,
    required this.controller,
    this.dica,
    this.validator,
    this.mascara,
    this.keyboardType,
    this.maxLines = 1,
    this.textCapitalization = TextCapitalization.none,
    this.enabled = true,
    this.ajuda,
  });

  final String rotulo;
  final TextEditingController controller;
  final String? dica;
  final FormFieldValidator<String>? validator;
  final TextInputFormatter? mascara;
  final TextInputType? keyboardType;
  final int maxLines;
  final TextCapitalization textCapitalization;
  final bool enabled;
  final String? ajuda;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RotuloCampo(rotulo),
        const SizedBox(height: 8),
        TextFormField(
          key: Key('campo_$rotulo'),
          controller: controller,
          validator: validator,
          enabled: enabled,
          keyboardType: keyboardType ?? (mascara != null ? TextInputType.number : null),
          inputFormatters: mascara == null ? null : [mascara!],
          maxLines: maxLines,
          textCapitalization: textCapitalization,
          autocorrect: false,
          textInputAction: maxLines > 1 ? TextInputAction.newline : TextInputAction.next,
          style: const TextStyle(fontSize: 14, color: AppColors.texto),
          decoration: InputDecoration(
            isDense: true,
            hintText: dica,
            helperText: ajuda,
            helperMaxLines: 2,
            contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          ),
        ),
      ],
    );
  }
}

/// Lista suspensa com rótulo fixo acima (mesmo visual do [CampoAdmin]).
class SelecaoAdmin<T> extends StatelessWidget {
  const SelecaoAdmin({
    super.key,
    required this.rotulo,
    required this.valor,
    required this.opcoes,
    required this.rotuloDe,
    required this.onChanged,
    this.validator,
    this.ajuda,
  });

  final String rotulo;
  final T? valor;
  final List<T> opcoes;
  final String Function(T) rotuloDe;
  final ValueChanged<T?> onChanged;
  final FormFieldValidator<T>? validator;
  final String? ajuda;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RotuloCampo(rotulo),
        const SizedBox(height: 8),
        DropdownButtonFormField<T>(
          key: Key('campo_$rotulo'),
          initialValue: valor,
          isExpanded: true,
          validator: validator,
          onChanged: onChanged,
          borderRadius: BorderRadius.circular(AppTheme.raioCampo),
          style: const TextStyle(fontSize: 14, color: AppColors.texto),
          decoration: InputDecoration(
            isDense: true,
            helperText: ajuda,
            helperMaxLines: 2,
            contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          ),
          items: [
            for (final o in opcoes)
              DropdownMenuItem<T>(value: o, child: Text(rotuloDe(o), overflow: TextOverflow.ellipsis)),
          ],
        ),
      ],
    );
  }
}

class RotuloCampo extends StatelessWidget {
  const RotuloCampo(this.texto, {super.key});

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Text(texto, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto));
  }
}

/// Linhas de campos: [colunas] lado a lado no desktop, um por linha abaixo de [larguraMinima].
class LinhaCampos extends StatelessWidget {
  const LinhaCampos({super.key, required this.campos, this.flex, this.larguraMinima = 640});

  final List<Widget> campos;
  final List<int>? flex;
  final double larguraMinima;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        if (limites.maxWidth < larguraMinima) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (var i = 0; i < campos.length; i++) ...[if (i > 0) const SizedBox(height: 16), campos[i]],
            ],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (var i = 0; i < campos.length; i++) ...[
              if (i > 0) const SizedBox(width: 16),
              Expanded(flex: flex?[i] ?? 1, child: campos[i]),
            ],
          ],
        );
      },
    );
  }
}

/// Diálogo de formulário das abas do Admin (cantos de 20 px, largura até 520 px).
class DialogoAdmin extends StatelessWidget {
  const DialogoAdmin({
    super.key,
    required this.titulo,
    required this.conteudo,
    required this.rotuloConfirmar,
    required this.onConfirmar,
    this.subtitulo,
    this.enviando = false,
  });

  final String titulo;
  final String? subtitulo;
  final Widget conteudo;
  final String rotuloConfirmar;
  final VoidCallback onConfirmar;
  final bool enviando;

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: AppColors.superficie,
      surfaceTintColor: Colors.transparent,
      insetPadding: const EdgeInsets.all(16),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppTheme.raioCard)),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(titulo, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.texto)),
              if (subtitulo != null) ...[
                const SizedBox(height: 6),
                Text(subtitulo!, style: const TextStyle(fontSize: 14, height: 1.4, color: AppColors.textoSuave)),
              ],
              const SizedBox(height: 20),
              conteudo,
              const SizedBox(height: 24),
              Wrap(
                alignment: WrapAlignment.end,
                spacing: 12,
                runSpacing: 12,
                children: [
                  BotaoAdmin(
                    rotulo: 'Cancelar',
                    onPressed: enviando ? null : () => Navigator.of(context).pop(),
                  ),
                  BotaoAdmin(
                    rotulo: enviando ? 'Salvando…' : rotuloConfirmar,
                    variante: VarianteBotao.primario,
                    onPressed: enviando ? null : onConfirmar,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Aviso rápido no rodapé (mesmo estilo em todas as abas).
void avisarAdmin(BuildContext context, String mensagem, {SnackBarAction? acao}) {
  final largura = MediaQuery.sizeOf(context).width;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(mensagem),
      action: acao,
      width: math.min(480, largura - 32),
      duration: const Duration(seconds: 4),
    ));
}
