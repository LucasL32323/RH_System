import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/formatting/mascara_entrada.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/validation/validadores.dart';
import '../../../domain/entities/compliance.dart';
import '../../viewmodels/compliance_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Central de Compliance (RF15, RF16, RF18, RF20): leituras obrigatórias com confirmação,
/// testes obrigatórios, testes de perfil (DISC) e vitrine de benefícios.
class AbaCompliance extends ConsumerWidget {
  const AbaCompliance({super.key});

  Future<void> _publicar(BuildContext context) async {
    final aviso = await showDialog<LeituraObrigatoria>(context: context, builder: (_) => const _DialogoAviso());
    if (aviso != null && context.mounted) {
      avisarAdmin(context, '"${aviso.titulo}" publicado para ${aviso.publico} colaboradores.');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(complianceViewModelProvider);

    return estado.when(
      loading: () => const EstadoCarregando(texto: 'Carregando a Central de Compliance…'),
      error: (_, __) => Padding(
        padding: const EdgeInsets.all(16),
        child: EstadoErro(
          titulo: 'Não foi possível carregar a Central de Compliance',
          onTentar: () => ref.invalidate(complianceViewModelProvider),
        ),
      ),
      data: (c) {
        final pendentesLeitura = c.leituras.fold<int>(0, (t, l) => t + l.pendentes);
        return PaginaAdmin(
          titulo: 'Central de Compliance',
          subtitulo: 'Leituras e testes obrigatórios, perfil comportamental e benefícios',
          acoes: [
            BotaoAdmin(
              rotulo: 'Publicar aviso',
              icone: Icons.campaign_rounded,
              variante: VarianteBotao.primario,
              onPressed: () => _publicar(context),
            ),
          ],
          children: [
            DuasColunas(
              larguraMinima: 1100,
              flexEsquerda: 1,
              flexDireita: 1,
              esquerda: _Leituras(leituras: c.leituras, pendentes: pendentesLeitura),
              direita: _Testes(testes: c.testes),
            ),
            const SizedBox(height: 24),
            DuasColunas(
              larguraMinima: 1100,
              flexEsquerda: 2,
              flexDireita: 3,
              esquerda: _Disc(perfil: c.perfilDisc),
              direita: _Beneficios(beneficios: c.beneficios),
            ),
          ],
        );
      },
    );
  }
}

/// Linha com título, detalhe, barra de adesão e percentual.
class _LinhaAdesao extends StatelessWidget {
  const _LinhaAdesao({
    required this.titulo,
    required this.detalhe,
    required this.fracao,
    required this.contagem,
    this.etiqueta,
  });

  final String titulo;
  final String detalhe;
  final double fracao;
  final String contagem;
  final Widget? etiqueta;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
      decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(titulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                    const SizedBox(height: 2),
                    Text(detalhe, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares)),
                  ],
                ),
              ),
              if (etiqueta != null) ...[const SizedBox(width: 8), etiqueta!],
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(child: BarraProgresso(fracao: fracao)),
              const SizedBox(width: 12),
              SizedBox(
                width: 108,
                child: Text(
                  contagem,
                  textAlign: TextAlign.right,
                  style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Leituras extends StatelessWidget {
  const _Leituras({required this.leituras, required this.pendentes});

  final List<LeituraObrigatoria> leituras;
  final int pendentes;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Leituras obrigatórias',
            subtitulo: 'Avisos com data de expiração e confirmação de leitura · $pendentes leituras pendentes',
            contagem: leituras.length,
          ),
          const SizedBox(height: 16),
          for (final l in leituras)
            _LinhaAdesao(
              titulo: l.titulo,
              detalhe: '${l.tipo} · publicado em ${Formatadores.diaMes(l.publicadoEm)} · expira em ${Formatadores.data(l.expiraEm)}',
              fracao: l.adesao,
              contagem: '${l.visualizacoes}/${l.publico} leram',
              etiqueta: l.visualizacoes == 0 ? const EtiquetaStatus(texto: 'Novo', tom: TomStatus.info, ponto: false) : null,
            ),
          const SizedBox(height: 8),
        ],
      ),
    );
  }
}

class _Testes extends StatelessWidget {
  const _Testes({required this.testes});

  final List<TesteObrigatorio> testes;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Testes obrigatórios',
            subtitulo: 'Formulários com perguntas obrigatórias respondidos pelo app',
            contagem: testes.length,
          ),
          const SizedBox(height: 16),
          for (final t in testes)
            _LinhaAdesao(
              titulo: t.titulo,
              detalhe: '${t.tipo} · ${t.perguntas} perguntas · prazo ${Formatadores.data(t.prazo)}',
              fracao: t.adesao,
              contagem: '${t.respondidos}/${t.publico} responderam',
              etiqueta: EtiquetaStatus(
                texto: Formatadores.percentual(t.adesao * 100, casas: 0),
                tom: t.adesao >= 0.8 ? TomStatus.sucesso : TomStatus.aviso,
                ponto: false,
              ),
            ),
          const SizedBox(height: 8),
        ],
      ),
    );
  }
}

class _Disc extends StatelessWidget {
  const _Disc({required this.perfil});

  final PerfilDisc perfil;

  @override
  Widget build(BuildContext context) {
    final total = perfil.respondidos == 0 ? 1 : perfil.respondidos;
    final fatias = [
      ('D', 'Dominância', perfil.dominancia),
      ('I', 'Influência', perfil.influencia),
      ('S', 'Estabilidade', perfil.estabilidade),
      ('C', 'Conformidade', perfil.conformidade),
    ];

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Teste de perfil (DISC)',
            subtitulo: '${perfil.respondidos} de ${perfil.publico} colaboradores responderam',
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
            child: Column(
              children: [
                for (final f in fatias)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 7),
                    child: Tooltip(
                      message: '${f.$2}: ${f.$3} pessoas',
                      child: Row(
                        children: [
                          Container(
                            width: 28,
                            height: 28,
                            alignment: Alignment.center,
                            decoration: BoxDecoration(color: AppColors.infoFundo, borderRadius: BorderRadius.circular(8)),
                            child: Text(f.$1, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: AppColors.primaria)),
                          ),
                          const SizedBox(width: 10),
                          SizedBox(width: 100, child: Text(f.$2, style: const TextStyle(fontSize: 14, color: AppColors.texto))),
                          Expanded(child: BarraProgresso(fracao: f.$3 / total)),
                          SizedBox(
                            width: 48,
                            child: Text(
                              Formatadores.percentual(f.$3 / total * 100, casas: 0),
                              textAlign: TextAlign.right,
                              style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Beneficios extends StatelessWidget {
  const _Beneficios({required this.beneficios});

  final List<Beneficio> beneficios;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Vitrine de benefícios',
            subtitulo: 'Benefícios da empresa e quantos colaboradores possuem cada um',
            contagem: beneficios.length,
          ),
          Padding(
            padding: const EdgeInsets.all(24),
            child: LayoutBuilder(
              builder: (context, limites) {
                final colunas = limites.maxWidth >= 560 ? 2 : 1;
                final largura = (limites.maxWidth - (colunas - 1) * 16) / colunas;
                return Wrap(
                  spacing: 16,
                  runSpacing: 16,
                  children: [
                    for (final b in beneficios)
                      Container(
                        width: largura,
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: AppColors.superficieSuave,
                          borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                const Icon(Icons.redeem_rounded, size: 18, color: AppColors.destaque),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(b.nome, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(b.descricao, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
                            const SizedBox(height: 10),
                            BarraProgresso(fracao: b.publico == 0 ? 0 : b.aderentes / b.publico, altura: 6),
                            const SizedBox(height: 6),
                            Text(
                              '${b.aderentes} de ${b.publico} possuem',
                              style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                            ),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _DialogoAviso extends ConsumerStatefulWidget {
  const _DialogoAviso();

  @override
  ConsumerState<_DialogoAviso> createState() => _DialogoAvisoState();
}

class _DialogoAvisoState extends ConsumerState<_DialogoAviso> {
  static const _tipos = ['Comunicado', 'Política', 'Norma'];

  final _form = GlobalKey<FormState>();
  final _titulo = TextEditingController();
  final _expira = TextEditingController();
  String _tipo = _tipos.first;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _titulo.dispose();
    _expira.dispose();
    super.dispose();
  }

  Future<void> _publicar() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final aviso = await ref.read(complianceViewModelProvider.notifier).publicarAviso(
            titulo: _titulo.text.trim(),
            tipo: _tipo,
            expiraEm: Formatadores.lerData(_expira.text)!,
          );
      if (mounted) Navigator.of(context).pop(aviso);
    } on Falha catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final hoje = DateTime.now();
    return DialogoAdmin(
      titulo: 'Publicar aviso',
      subtitulo: 'Todos os colaboradores ativos recebem; o sistema registra quem já leu.',
      rotuloConfirmar: 'Publicar',
      enviando: _enviando,
      onConfirmar: _publicar,
      conteudo: Form(
        key: _form,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_erro != null) ...[
              Text(_erro!, style: const TextStyle(fontSize: 14, color: AppColors.erro)),
              const SizedBox(height: 12),
            ],
            CampoAdmin(
              rotulo: 'Título do aviso',
              controller: _titulo,
              textCapitalization: TextCapitalization.sentences,
              validator: (v) => Validadores.obrigatorio(v, 'Informe o título.'),
            ),
            const SizedBox(height: 16),
            LinhaCampos(
              larguraMinima: 420,
              campos: [
                SelecaoAdmin<String>(
                  rotulo: 'Tipo',
                  valor: _tipo,
                  opcoes: _tipos,
                  rotuloDe: (t) => t,
                  onChanged: (t) => setState(() => _tipo = t ?? _tipos.first),
                ),
                CampoAdmin(
                  rotulo: 'Expira em',
                  controller: _expira,
                  dica: 'dd/mm/aaaa',
                  mascara: MascaraEntrada.data,
                  validator: (v) {
                    final erro = Validadores.data(v, rotulo: 'data de expiração');
                    if (erro != null) return erro;
                    final data = Formatadores.lerData(v!)!;
                    return data.isBefore(DateTime(hoje.year, hoje.month, hoje.day)) ? 'A data já passou.' : null;
                  },
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
