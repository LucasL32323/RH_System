import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/colaborador_resumo.dart';

/// Peças visuais compartilhadas pelas abas do Admin: painel (card de 20 px), cabeçalho de painel,
/// etiqueta de status, célula de colaborador, botão compacto e tabela com rolagem horizontal.

/// Números alinhados em coluna (valores de KPI, datas e contagens).
const List<FontFeature> numerosTabulares = [FontFeature.tabularFigures()];

/// Cor de fundo e de texto de cada estado. Semântica separada da cor da marca.
enum TomStatus {
  sucesso(AppColors.sucessoFundo, AppColors.sucesso),
  aviso(AppColors.avisoFundo, AppColors.aviso),
  erro(AppColors.erroFundo, AppColors.erro),
  info(AppColors.infoFundo, AppColors.primaria),
  neutro(AppColors.superficieSuave, AppColors.textoSuave);

  const TomStatus(this.fundo, this.texto);
  final Color fundo;
  final Color texto;
}

/// Card do dashboard: mesma borda e cantos do card do login, sombra mais curta
/// (o dashboard tem muitos cards lado a lado).
const BoxDecoration decoracaoPainel = BoxDecoration(
  color: AppColors.superficie,
  borderRadius: BorderRadius.all(Radius.circular(AppTheme.raioCard)),
  border: Border.fromBorderSide(BorderSide(color: AppColors.borda)),
  boxShadow: [
    BoxShadow(color: AppColors.sombraFina, blurRadius: 2, offset: Offset(0, 1)),
    BoxShadow(color: AppColors.sombraCard, blurRadius: 24, spreadRadius: -12, offset: Offset(0, 10)),
  ],
);

class PainelAdmin extends StatelessWidget {
  const PainelAdmin({super.key, required this.child, this.padding = EdgeInsets.zero});

  final Widget child;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    // Material transparente: ListTile/InkWell dentro do painel desenham o realce sobre o fundo do card.
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: decoracaoPainel,
      child: Material(
        type: MaterialType.transparency,
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}

/// Título + subtítulo (+ contador) à esquerda e ação à direita; empilha em painéis estreitos.
class CabecalhoPainel extends StatelessWidget {
  const CabecalhoPainel({super.key, required this.titulo, this.subtitulo, this.contagem, this.acao});

  final String titulo;
  final String? subtitulo;
  final int? contagem;
  final Widget? acao;

  @override
  Widget build(BuildContext context) {
    final textos = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Flexible(
              child: Text(
                titulo,
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.texto),
              ),
            ),
            if (contagem != null) ...[
              const SizedBox(width: 8),
              Contador(valor: contagem!),
            ],
          ],
        ),
        if (subtitulo != null) ...[
          const SizedBox(height: 4),
          Text(subtitulo!, style: const TextStyle(fontSize: 14, height: 1.4, color: AppColors.textoSuave)),
        ],
      ],
    );

    return Padding(
      padding: const EdgeInsets.fromLTRB(24, 24, 24, 0),
      child: LayoutBuilder(
        builder: (context, limites) {
          if (acao == null) return textos;
          if (limites.maxWidth < 560) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [textos, const SizedBox(height: 12), acao!],
            );
          }
          return Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [Expanded(child: textos), const SizedBox(width: 16), acao!],
          );
        },
      ),
    );
  }
}

/// Contador arredondado ao lado de títulos e abas.
class Contador extends StatelessWidget {
  const Contador({super.key, required this.valor, this.ativo = true});

  final int valor;
  final bool ativo;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: ativo ? AppColors.infoFundo : AppColors.superficieSuave,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        '$valor',
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: ativo ? AppColors.primaria : AppColors.textoSuave,
          fontFeatures: numerosTabulares,
        ),
      ),
    );
  }
}

/// Etiqueta de status ("Concluída", "Em andamento", "Aprovada").
class EtiquetaStatus extends StatelessWidget {
  const EtiquetaStatus({super.key, required this.texto, required this.tom, this.ponto = true});

  final String texto;
  final TomStatus tom;
  final bool ponto;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: tom.fundo, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (ponto) ...[
            Container(width: 6, height: 6, decoration: BoxDecoration(color: tom.texto, shape: BoxShape.circle)),
            const SizedBox(width: 6),
          ],
          Flexible(
            child: Text(
              texto,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: tom.texto, fontFeatures: numerosTabulares),
            ),
          ),
        ],
      ),
    );
  }
}

/// Avatar com iniciais + nome + "matrícula · departamento".
class CelulaColaborador extends StatelessWidget {
  const CelulaColaborador({super.key, required this.colaborador});

  final ColaboradorResumo colaborador;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        AvatarIniciais(iniciais: colaborador.iniciais),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                colaborador.nome,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto),
              ),
              const SizedBox(height: 2),
              Text(
                '${colaborador.matricula} · ${colaborador.departamento}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class AvatarIniciais extends StatelessWidget {
  const AvatarIniciais({
    super.key,
    required this.iniciais,
    this.tamanho = 36,
    this.fundo = AppColors.infoFundo,
    this.cor = AppColors.primaria,
  });

  final String iniciais;
  final double tamanho;
  final Color fundo;
  final Color cor;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: tamanho,
      height: tamanho,
      alignment: Alignment.center,
      decoration: BoxDecoration(color: fundo, shape: BoxShape.circle),
      child: Text(iniciais, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: cor)),
    );
  }
}

enum VarianteBotao { primario, contorno, texto }

/// Botão de 40 px (36 px compacto). Os botões do tema têm largura total (formulário do login),
/// então o dashboard usa este, que se ajusta ao conteúdo.
class BotaoAdmin extends StatelessWidget {
  const BotaoAdmin({
    super.key,
    required this.rotulo,
    required this.onPressed,
    this.icone,
    this.iconeFinal,
    this.variante = VarianteBotao.contorno,
    this.compacto = false,
  });

  final String rotulo;
  final VoidCallback? onPressed;
  final IconData? icone;
  final IconData? iconeFinal;
  final VarianteBotao variante;
  final bool compacto;

  @override
  Widget build(BuildContext context) {
    final tamanho = Size(0, compacto ? 36 : 40);
    final padding = EdgeInsets.symmetric(horizontal: compacto ? 12 : 16);
    const forma = RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(AppTheme.raioCampo)));
    const texto = TextStyle(fontSize: 14, fontWeight: FontWeight.w600);

    final conteudo = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (icone != null) ...[Icon(icone, size: 18), const SizedBox(width: 8)],
        Flexible(child: Text(rotulo, maxLines: 1, overflow: TextOverflow.ellipsis)),
        if (iconeFinal != null) ...[const SizedBox(width: 6), Icon(iconeFinal, size: 18)],
      ],
    );

    switch (variante) {
      case VarianteBotao.primario:
        return FilledButton(
          onPressed: onPressed,
          style: FilledButton.styleFrom(
            minimumSize: tamanho,
            padding: padding,
            shape: forma,
            textStyle: texto,
            backgroundColor: AppColors.primaria,
            foregroundColor: Colors.white,
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
          ).copyWith(
            overlayColor: const WidgetStatePropertyAll(AppColors.brancoTransl12),
          ),
          child: conteudo,
        );
      case VarianteBotao.contorno:
        return OutlinedButton(
          onPressed: onPressed,
          style: OutlinedButton.styleFrom(
            minimumSize: tamanho,
            padding: padding,
            shape: forma,
            textStyle: texto,
            foregroundColor: AppColors.texto,
            backgroundColor: AppColors.superficie,
            side: const BorderSide(color: AppColors.borda),
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
          ),
          child: conteudo,
        );
      case VarianteBotao.texto:
        return TextButton(
          onPressed: onPressed,
          style: TextButton.styleFrom(
            minimumSize: tamanho,
            padding: padding,
            shape: forma,
            textStyle: texto,
            foregroundColor: AppColors.primaria,
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
          ),
          child: conteudo,
        );
    }
  }
}

class ColunaTabela {
  const ColunaTabela(this.rotulo, {this.flex = 1, this.alinhamento = Alignment.centerLeft});

  final String rotulo;
  final int flex;
  final Alignment alinhamento;
}

/// Tabela do dashboard: cabeçalho em caixa-alta sobre fundo suave, linhas com borda superior
/// e realce no hover. Abaixo de [larguraMinima] rola na horizontal dentro do painel.
class TabelaAdmin extends StatelessWidget {
  const TabelaAdmin({
    super.key,
    required this.colunas,
    required this.linhas,
    required this.larguraMinima,
    required this.mensagemVazia,
  });

  final List<ColunaTabela> colunas;
  final List<List<Widget>> linhas;
  final double larguraMinima;
  final String mensagemVazia;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final largura = math.max(limites.maxWidth, larguraMinima);
        return SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: SizedBox(
            width: largura,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  height: 44,
                  color: AppColors.superficieSuave,
                  child: _linha([
                    for (final c in colunas)
                      Text(
                        c.rotulo.toUpperCase(),
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          letterSpacing: 0.6,
                          color: AppColors.textoSuave,
                        ),
                      ),
                  ]),
                ),
                if (linhas.isEmpty)
                  Container(
                    padding: const EdgeInsets.symmetric(vertical: 40, horizontal: 24),
                    decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
                    child: Text(
                      mensagemVazia,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontSize: 14, color: AppColors.textoSuave),
                    ),
                  )
                else
                  for (final celulas in linhas) _LinhaComRealce(child: _linha(celulas)),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _linha(List<Widget> celulas) {
    return Row(
      children: [
        for (var i = 0; i < colunas.length; i++)
          Expanded(
            flex: colunas[i].flex,
            child: Padding(
              padding: EdgeInsets.only(left: i == 0 ? 24 : 16, right: i == colunas.length - 1 ? 24 : 16),
              child: Align(alignment: colunas[i].alinhamento, child: celulas[i]),
            ),
          ),
      ],
    );
  }
}

class _LinhaComRealce extends StatefulWidget {
  const _LinhaComRealce({required this.child});

  final Widget child;

  @override
  State<_LinhaComRealce> createState() => _LinhaComRealceState();
}

class _LinhaComRealceState extends State<_LinhaComRealce> {
  bool _sobre = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      onEnter: (_) => setState(() => _sobre = true),
      onExit: (_) => setState(() => _sobre = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          color: _sobre ? AppColors.superficieSuave : AppColors.superficie,
          border: const Border(top: BorderSide(color: AppColors.borda)),
        ),
        child: widget.child,
      ),
    );
  }
}
