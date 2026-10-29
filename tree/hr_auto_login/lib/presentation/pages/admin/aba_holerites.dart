import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/files/arquivos.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/formatting/mascara_entrada.dart';
import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/holerite.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../../domain/regras/regras_holerite.dart';
import '../../providers.dart';
import '../../viewmodels/holerites_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Holerites (RF29, RF30, RNF08, RNF17).
///  - Todos os perfis: "Meus holerites" com download do PDF.
///  - RH e Admin: consulta da equipe pelo código FUNC (nunca pelo nome) e publicação de PDF por competência.
/// O servidor repete todas as regras e audita o download do holerite de outra pessoa.
class AbaHolerites extends ConsumerWidget {
  const AbaHolerites({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  bool get _gestor => usuario.perfil == 'RH' || usuario.perfil == 'Admin';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return PaginaAdmin(
      titulo: 'Holerites',
      subtitulo: _gestor
          ? '${usuario.nome} · ${usuario.codigo} · seus holerites, consulta da equipe e publicação de PDF'
          : '${usuario.nome} · ${usuario.codigo} · seus contracheques em PDF',
      children: [
        const _PainelMeusHolerites(),
        if (_gestor) ...[
          const SizedBox(height: 24),
          const DuasColunas(
            flexEsquerda: 3,
            flexDireita: 2,
            esquerda: _PainelConsultaEquipe(),
            direita: _PainelPublicar(),
          ),
        ],
      ],
    );
  }
}

/// Mensagem para o usuário a partir de qualquer erro (só a [Falha] traz texto próprio).
String _mensagemDe(Object erro, String padrao) => erro is Falha ? erro.mensagem : padrao;

/// "Setembro de 2026"
String _tituloCompetencia(Holerite h) {
  final nome = Formatadores.nomeMes(h.mes);
  return '${nome[0].toUpperCase()}${nome.substring(1)} de ${h.ano}';
}

/// Competência repetida: a API responde 409; o protótipo, uma Falha que começa com "Já existe".
bool _ehConflito(Falha falha) =>
    (falha is ApiException && falha.statusCode == 409) || falha.mensagem.startsWith('Já existe');

Widget _caixaDeErro(String mensagem, {Key? key}) => Container(
      key: key,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: AppColors.erroFundo, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
      child: Text(mensagem, style: const TextStyle(fontSize: 14, color: AppColors.erro)),
    );

/// Tabela de holerites com o botão "Baixar PDF" em cada linha.
/// [prefixo] diferencia as chaves dos botões entre "Meus holerites" e a consulta da equipe.
class _TabelaHolerites extends StatelessWidget {
  const _TabelaHolerites({
    required this.itens,
    required this.prefixo,
    required this.onBaixar,
    required this.mensagemVazia,
  });

  final List<Holerite> itens;
  final String prefixo;
  final Future<String> Function(Holerite) onBaixar;
  final String mensagemVazia;

  static const _colunas = [
    ColunaTabela('Competência', flex: 20),
    ColunaTabela('Publicado em', flex: 14),
    ColunaTabela('Arquivo', flex: 22),
    ColunaTabela('', flex: 16, alinhamento: Alignment.centerRight),
  ];

  @override
  Widget build(BuildContext context) {
    return TabelaAdmin(
      colunas: _colunas,
      larguraMinima: 560,
      mensagemVazia: mensagemVazia,
      linhas: [
        for (final h in itens)
          [
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  _tituloCompetencia(h),
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                ),
                const SizedBox(height: 2),
                Text(
                  h.competencia,
                  style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                ),
              ],
            ),
            Text(
              Formatadores.data(h.dataPublicacao),
              style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
            ),
            Row(
              children: [
                const Icon(Icons.picture_as_pdf_rounded, size: 18, color: AppColors.erro),
                const SizedBox(width: 8),
                Flexible(
                  child: Text(
                    h.nomeArquivo,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 13, color: AppColors.textoSuave),
                  ),
                ),
              ],
            ),
            _BotaoBaixar(key: Key('baixar_$prefixo${h.id}'), holerite: h, onBaixar: onBaixar),
          ],
      ],
    );
  }
}

/// Botão "Baixar PDF" de uma linha: mostra "Baixando…" e avisa o resultado.
class _BotaoBaixar extends StatefulWidget {
  const _BotaoBaixar({super.key, required this.holerite, required this.onBaixar});

  final Holerite holerite;
  final Future<String> Function(Holerite) onBaixar;

  @override
  State<_BotaoBaixar> createState() => _BotaoBaixarState();
}

class _BotaoBaixarState extends State<_BotaoBaixar> {
  bool _baixando = false;

  Future<void> _baixar() async {
    setState(() => _baixando = true);
    String? aviso;
    try {
      await widget.onBaixar(widget.holerite);
      aviso = 'Holerite ${widget.holerite.competencia} baixado.';
    } on Falha catch (e) {
      aviso = e.mensagem;
    } catch (_) {
      aviso = 'Não foi possível baixar o holerite. Tente novamente.';
    }
    if (!mounted) return;
    setState(() => _baixando = false);
    avisarAdmin(context, aviso);
  }

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: 'Baixar holerite ${widget.holerite.competencia}',
      child: BotaoAdmin(
        rotulo: _baixando ? 'Baixando…' : 'Baixar PDF',
        icone: Icons.download_rounded,
        compacto: true,
        onPressed: _baixando ? null : _baixar,
      ),
    );
  }
}

/// "Meus holerites": os de quem está logado.
class _PainelMeusHolerites extends ConsumerWidget {
  const _PainelMeusHolerites();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(holeritesViewModelProvider);
    final viewModel = ref.read(holeritesViewModelProvider.notifier);

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Meus holerites',
            subtitulo: 'Do mais recente para o mais antigo. Só você e o RH têm acesso aos PDFs.',
            contagem: estado.valueOrNull?.length,
            acao: BotaoAdmin(
              rotulo: 'Atualizar',
              icone: Icons.refresh_rounded,
              compacto: true,
              onPressed: estado.isLoading ? null : viewModel.recarregar,
            ),
          ),
          const SizedBox(height: 20),
          estado.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (erro, _) => Padding(
              padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _caixaDeErro(_mensagemDe(erro, 'Não foi possível carregar os holerites. Tente novamente.')),
                  const SizedBox(height: 12),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: BotaoAdmin(
                      key: const Key('tentar_meus_holerites'),
                      rotulo: 'Tentar novamente',
                      icone: Icons.refresh_rounded,
                      onPressed: viewModel.recarregar,
                    ),
                  ),
                ],
              ),
            ),
            data: (lista) => _TabelaHolerites(
              itens: lista,
              prefixo: 'meus_',
              onBaixar: viewModel.baixar,
              mensagemVazia: 'Você ainda não tem holerites publicados.',
            ),
          ),
        ],
      ),
    );
  }
}

/// Consulta de holerites de outro funcionário pelo código FUNC (RH/Admin).
class _PainelConsultaEquipe extends ConsumerStatefulWidget {
  const _PainelConsultaEquipe();

  @override
  ConsumerState<_PainelConsultaEquipe> createState() => _PainelConsultaEquipeState();
}

class _PainelConsultaEquipeState extends ConsumerState<_PainelConsultaEquipe> {
  final _form = GlobalKey<FormState>();
  final _codigo = TextEditingController();

  @override
  void dispose() {
    _codigo.dispose();
    super.dispose();
  }

  String? _validarCodigo(String? valor) {
    if ((valor ?? '').trim().isEmpty) return 'Informe o código do funcionário.';
    if (RegrasHolerite.normalizarCodigo(valor) == null) return 'Use o formato FUNC-0001 (ou só o número).';
    return null;
  }

  Future<void> _consultar() async {
    if (!_form.currentState!.validate()) return;
    await ref.read(consultaHoleritesViewModelProvider.notifier).consultar(_codigo.text);
  }

  void _limpar() {
    _codigo.clear();
    ref.read(consultaHoleritesViewModelProvider.notifier).limpar();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(consultaHoleritesViewModelProvider);
    final viewModel = ref.read(consultaHoleritesViewModelProvider.notifier);

    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(
            titulo: 'Holerites da equipe',
            subtitulo: 'Busca pelo código FUNC. Cada PDF baixado de outra pessoa fica registrado na auditoria (LGPD).',
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 20, 24, 20),
            child: Form(
              key: _form,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  CampoAdmin(
                    rotulo: 'Código FUNC',
                    controller: _codigo,
                    dica: 'FUNC-0004',
                    validator: _validarCodigo,
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      BotaoAdmin(
                        key: const Key('botao_consultar_holerites'),
                        rotulo: estado.isLoading ? 'Consultando…' : 'Consultar',
                        icone: Icons.search_rounded,
                        variante: VarianteBotao.primario,
                        onPressed: estado.isLoading ? null : _consultar,
                      ),
                      if (estado.valueOrNull != null || estado.hasError)
                        BotaoAdmin(rotulo: 'Limpar', onPressed: estado.isLoading ? null : _limpar),
                    ],
                  ),
                ],
              ),
            ),
          ),
          estado.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (erro, _) => Padding(
              padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
              child: _caixaDeErro(
                _mensagemDe(erro, 'Não foi possível consultar os holerites. Tente novamente.'),
                key: const Key('erro_consulta_holerites'),
              ),
            ),
            data: (consulta) {
              if (consulta == null) {
                return const Padding(
                  padding: EdgeInsets.fromLTRB(24, 0, 24, 24),
                  child: Text(
                    'Digite o código do funcionário para ver os holerites publicados.',
                    style: TextStyle(fontSize: 14, color: AppColors.textoSuave),
                  ),
                );
              }
              final quem = consulta.nome.isEmpty ? consulta.codigo : '${consulta.nome} · ${consulta.codigo}';
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 0, 24, 12),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            quem,
                            key: const Key('titulo_consulta_holerites'),
                            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                          ),
                        ),
                        Contador(valor: consulta.holerites.length),
                      ],
                    ),
                  ),
                  _TabelaHolerites(
                    itens: consulta.holerites,
                    prefixo: 'equipe_',
                    onBaixar: viewModel.baixar,
                    mensagemVazia: '${consulta.codigo} ainda não tem holerites publicados.',
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

/// Publicação de PDF por competência (RF29, RH/Admin). O PDF vai direto para a API, que valida de novo.
class _PainelPublicar extends ConsumerStatefulWidget {
  const _PainelPublicar();

  @override
  ConsumerState<_PainelPublicar> createState() => _PainelPublicarState();
}

class _PainelPublicarState extends ConsumerState<_PainelPublicar> {
  final _form = GlobalKey<FormState>();
  final _codigo = TextEditingController();
  late final TextEditingController _ano;
  late int _mes;

  ArquivoSelecionado? _arquivo;
  bool _substituir = false;
  String? _erro;
  bool _conflito = false;

  @override
  void initState() {
    super.initState();
    // Começa no mês anterior, que é o da folha normalmente publicada.
    final anterior = DateTime(DateTime.now().year, DateTime.now().month - 1);
    _mes = anterior.month;
    _ano = TextEditingController(text: '${anterior.year}');
  }

  @override
  void dispose() {
    _codigo.dispose();
    _ano.dispose();
    super.dispose();
  }

  Future<void> _escolherArquivo() async {
    try {
      final escolhido = await ref.read(seletorArquivoProvider).escolherPdf();
      if (escolhido == null || !mounted) return; // cancelou
      RegrasHolerite.validarArquivo(escolhido.nome, escolhido.bytes);
      setState(() {
        _arquivo = escolhido;
        _erro = null;
        _conflito = false;
      });
    } on Falha catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    } catch (_) {
      if (mounted) setState(() => _erro = 'Não foi possível escolher o arquivo. Tente novamente.');
    }
  }

  String? _validarCodigo(String? valor) {
    if ((valor ?? '').trim().isEmpty) return 'Informe o código do funcionário.';
    if (RegrasHolerite.normalizarCodigo(valor) == null) return 'Use o formato FUNC-0001 (ou só o número).';
    return null;
  }

  String? _validarAno(String? valor) {
    final ano = int.tryParse((valor ?? '').trim());
    final limite = DateTime.now().year;
    if (ano == null || ano < RegrasHolerite.anoMinimo || ano > limite) {
      return 'Informe um ano entre ${RegrasHolerite.anoMinimo} e $limite.';
    }
    return null;
  }

  Future<void> _publicar({bool substituir = false}) async {
    if (!_form.currentState!.validate()) return;
    final arquivo = _arquivo;
    if (arquivo == null) {
      setState(() {
        _erro = 'Escolha o PDF do holerite.';
        _conflito = false;
      });
      return;
    }
    setState(() {
      _erro = null;
      _conflito = false;
    });

    try {
      final resultado = await ref.read(publicarHoleriteViewModelProvider.notifier).publicar(
            codigo: _codigo.text,
            mes: _mes,
            ano: int.parse(_ano.text.trim()),
            arquivo: arquivo,
            substituir: substituir || _substituir,
          );
      if (!mounted) return;
      setState(() {
        _arquivo = null;
        _substituir = false;
      });
      final h = resultado.holerite;
      avisarAdmin(
        context,
        resultado.substituido
            ? 'Holerite ${h.competencia} de ${h.codigoFuncionario} substituído.'
            : 'Holerite ${h.competencia} de ${h.codigoFuncionario} publicado.',
      );
    } on Falha catch (e) {
      if (mounted) {
        setState(() {
          _erro = e.mensagem;
          _conflito = _ehConflito(e);
        });
      }
    } catch (_) {
      if (mounted) setState(() => _erro = 'Não foi possível publicar o holerite. Tente novamente.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final enviando = ref.watch(publicarHoleriteViewModelProvider);
    final arquivo = _arquivo;

    return PainelAdmin(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: _form,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Publicar holerite', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.texto)),
            const SizedBox(height: 4),
            const Text(
              'PDF de até 5 MB. Fica disponível para o funcionário e o RH.',
              style: TextStyle(fontSize: 14, height: 1.4, color: AppColors.textoSuave),
            ),
            const SizedBox(height: 20),
            if (_erro != null) ...[
              _caixaDeErro(_erro!, key: const Key('erro_publicar_holerite')),
              if (_conflito) ...[
                const SizedBox(height: 8),
                Align(
                  alignment: Alignment.centerLeft,
                  child: BotaoAdmin(
                    key: const Key('botao_substituir_e_publicar'),
                    rotulo: 'Substituir e publicar',
                    icone: Icons.swap_horiz_rounded,
                    variante: VarianteBotao.texto,
                    compacto: true,
                    onPressed: enviando ? null : () => _publicar(substituir: true),
                  ),
                ),
              ],
              const SizedBox(height: 16),
            ],
            CampoAdmin(
              rotulo: 'Código do funcionário',
              controller: _codigo,
              dica: 'FUNC-0004',
              validator: _validarCodigo,
              enabled: !enviando,
            ),
            const SizedBox(height: 16),
            LinhaCampos(
              larguraMinima: 380,
              flex: const [3, 2],
              campos: [
                SelecaoAdmin<int>(
                  rotulo: 'Mês',
                  valor: _mes,
                  opcoes: List<int>.generate(12, (i) => i + 1),
                  rotuloDe: (m) => '${Formatadores.nomeMes(m)[0].toUpperCase()}${Formatadores.nomeMes(m).substring(1)}',
                  onChanged: (m) => setState(() => _mes = m ?? _mes),
                ),
                CampoAdmin(
                  rotulo: 'Ano',
                  controller: _ano,
                  dica: 'aaaa',
                  mascara: MascaraEntrada('####'),
                  validator: _validarAno,
                  enabled: !enviando,
                ),
              ],
            ),
            const SizedBox(height: 16),
            const RotuloCampo('Arquivo PDF'),
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.superficieSuave,
                borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                border: Border.all(color: AppColors.borda),
              ),
              child: Row(
                children: [
                  Icon(
                    arquivo == null ? Icons.upload_file_rounded : Icons.picture_as_pdf_rounded,
                    size: 20,
                    color: arquivo == null ? AppColors.textoSuave : AppColors.erro,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      arquivo == null ? 'Nenhum arquivo escolhido' : '${arquivo.nome} · ${RegrasHolerite.tamanhoLegivel(arquivo.tamanho)}',
                      key: const Key('arquivo_escolhido'),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: arquivo == null ? AppColors.textoSuave : AppColors.texto),
                    ),
                  ),
                  if (arquivo != null)
                    IconButton(
                      tooltip: 'Remover arquivo',
                      onPressed: enviando ? null : () => setState(() => _arquivo = null),
                      icon: const Icon(Icons.close_rounded, size: 18),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: BotaoAdmin(
                key: const Key('botao_escolher_pdf'),
                rotulo: arquivo == null ? 'Escolher PDF' : 'Trocar PDF',
                icone: Icons.attach_file_rounded,
                compacto: true,
                onPressed: enviando ? null : _escolherArquivo,
              ),
            ),
            const SizedBox(height: 8),
            SwitchListTile(
              key: const Key('chave_substituir'),
              contentPadding: EdgeInsets.zero,
              value: _substituir,
              onChanged: enviando ? null : (v) => setState(() => _substituir = v),
              title: const Text('Substituir se já existir', style: TextStyle(fontSize: 14, color: AppColors.texto)),
              subtitle: const Text(
                'Troca o PDF da competência e registra a alteração na auditoria.',
                style: TextStyle(fontSize: 12, color: AppColors.textoSuave),
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              height: 48,
              child: FilledButton.icon(
                key: const Key('botao_publicar_holerite'),
                onPressed: enviando ? null : _publicar,
                icon: const Icon(Icons.publish_rounded),
                label: Text(enviando ? 'Publicando…' : 'Publicar holerite'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
