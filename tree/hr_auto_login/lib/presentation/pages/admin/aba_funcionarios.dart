import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/funcionario.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../viewmodels/funcionarios_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/dialogo_ficha_funcionario.dart';
import '../../widgets/admin/pagina_admin.dart';

enum _FiltroSituacao {
  ativos('Ativos'),
  inativos('Inativos'),
  todos('Todos');

  const _FiltroSituacao(this.rotulo);
  final String rotulo;
}

/// Funcionários (RF04, RF05, RF22): busca pelo código FUNC, lista com CPF mascarado,
/// consulta do CPF completo registrada (LGPD) e inativação sem apagar o cadastro.
class AbaFuncionarios extends ConsumerStatefulWidget {
  const AbaFuncionarios({super.key, required this.usuario, required this.onNovo, this.destaque});

  final UsuarioAutenticado usuario;
  final VoidCallback onNovo;

  /// Código recém-cadastrado (mostra o aviso de sucesso e a etiqueta "Novo").
  final String? destaque;

  @override
  ConsumerState<AbaFuncionarios> createState() => _AbaFuncionariosState();
}

class _AbaFuncionariosState extends ConsumerState<AbaFuncionarios> {
  static const _porPagina = 20;

  final _busca = TextEditingController();
  String? _setor; // null = todos
  _FiltroSituacao _situacao = _FiltroSituacao.ativos;
  int _pagina = 0;
  final Set<int> _cpfVisivel = {};

  @override
  void initState() {
    super.initState();
    _busca.addListener(() => setState(() => _pagina = 0));
  }

  @override
  void dispose() {
    _busca.dispose();
    super.dispose();
  }

  /// RF05: a busca é pelo código, não pelo nome. Aceita "FUNC-0142", "0142" ou "142".
  String get _digitosBusca => Formatadores.digitos(_busca.text);
  bool get _buscaPorNome => _busca.text.trim().isNotEmpty && _digitosBusca.isEmpty;

  List<Funcionario> _filtrar(List<Funcionario> todos) {
    final digitos = _digitosBusca;
    return todos.where((f) {
      if (_situacao == _FiltroSituacao.ativos && !f.ativo) return false;
      if (_situacao == _FiltroSituacao.inativos && f.ativo) return false;
      if (_setor != null && f.setor != _setor) return false;
      if (_buscaPorNome) return false;
      if (digitos.isNotEmpty) {
        final codigo = Formatadores.digitos(f.codigo);
        return codigo.contains(digitos) || int.tryParse(digitos) == f.id;
      }
      return true;
    }).toList();
  }

  String get _consultadoPor => '${widget.usuario.codigo} · ${widget.usuario.nome}';

  /// Olho do CPF: busca a ficha (na API é essa chamada que audita a consulta, LGPD/RF22) e mostra o CPF completo.
  Future<void> _mostrarCpf(Funcionario f) async {
    if (_cpfVisivel.contains(f.id)) {
      setState(() => _cpfVisivel.remove(f.id));
      return;
    }
    try {
      await ref.read(funcionariosViewModelProvider.notifier).consultarFicha(f, consultadoPor: _consultadoPor);
    } on Falha catch (e) {
      if (mounted) avisarAdmin(context, e.mensagem);
      return;
    }
    if (!mounted) return;
    setState(() => _cpfVisivel.add(f.id));
    avisarAdmin(context, 'Consulta ao CPF de ${f.nome} registrada (LGPD).');
  }

  /// Menu "Ver dados completos": CPF, RG, nascimento, telefone e residência (também auditado).
  Future<void> _verFicha(Funcionario f) async {
    final Funcionario ficha;
    try {
      ficha = await ref.read(funcionariosViewModelProvider.notifier).consultarFicha(f, consultadoPor: _consultadoPor);
    } on Falha catch (e) {
      if (mounted) avisarAdmin(context, e.mensagem);
      return;
    }
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (_) => DialogoFichaFuncionario(funcionario: ficha),
    );
  }

  Future<void> _alterarSituacao(Funcionario f) async {
    final inativar = f.ativo;
    final confirmado = await showDialog<bool>(
      context: context,
      builder: (contexto) => DialogoAdmin(
        titulo: inativar ? 'Inativar ${f.nome}?' : 'Reativar ${f.nome}?',
        subtitulo: inativar
            ? 'O acesso ao app é bloqueado. O cadastro, o código ${f.codigo} e o histórico ficam guardados.'
            : 'O acesso ao app é liberado de novo com o mesmo código ${f.codigo}.',
        rotuloConfirmar: inativar ? 'Inativar' : 'Reativar',
        onConfirmar: () => Navigator.of(contexto).pop(true),
        conteudo: const SizedBox.shrink(),
      ),
    );
    if (confirmado != true) return;
    try {
      await ref.read(funcionariosViewModelProvider.notifier).alterarSituacao(f, ativo: !inativar);
      if (mounted) avisarAdmin(context, '${f.codigo} ${inativar ? 'inativado' : 'reativado'}.');
    } on Falha catch (e) {
      if (mounted) avisarAdmin(context, e.mensagem);
    }
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(funcionariosViewModelProvider);
    final estrutura = ref.watch(estruturaViewModelProvider).valueOrNull;

    return estado.when(
      loading: () => const EstadoCarregando(texto: 'Carregando funcionários…'),
      error: (_, __) => Padding(
        padding: const EdgeInsets.all(16),
        child: EstadoErro(
          titulo: 'Não foi possível carregar os funcionários',
          onTentar: () => ref.invalidate(funcionariosViewModelProvider),
        ),
      ),
      data: (todos) {
        final ativos = todos.where((f) => f.ativo).length;
        final filtrados = _filtrar(todos);
        final paginas = math.max(1, (filtrados.length / _porPagina).ceil());
        final pagina = math.min(_pagina, paginas - 1);
        final inicio = pagina * _porPagina;
        final visiveis = filtrados.skip(inicio).take(_porPagina).toList();
        final setores = estrutura?.setores.map((s) => s.nome).toList() ??
            (todos.map((f) => f.setor).toSet().toList()..sort());

        return PaginaAdmin(
          titulo: 'Funcionários',
          subtitulo: '$ativos ativos · ${todos.length - ativos} inativos · busca pelo código FUNC',
          acoes: [
            BotaoAdmin(
              rotulo: 'Novo funcionário',
              icone: Icons.person_add_alt_1_rounded,
              variante: VarianteBotao.primario,
              onPressed: widget.onNovo,
            ),
          ],
          children: [
            if (widget.destaque != null) ...[
              _AvisoSucesso(texto: '${widget.destaque} cadastrado. O código é definitivo e não pode ser alterado.'),
              const SizedBox(height: 16),
            ],
            PainelAdmin(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 24, 24, 0),
                    child: _Filtros(
                      busca: _busca,
                      setores: setores,
                      setor: _setor,
                      situacao: _situacao,
                      onSetor: (s) => setState(() {
                        _setor = s;
                        _pagina = 0;
                      }),
                      onSituacao: (s) => setState(() {
                        _situacao = s;
                        _pagina = 0;
                      }),
                    ),
                  ),
                  if (_buscaPorNome)
                    const Padding(
                      padding: EdgeInsets.fromLTRB(24, 12, 24, 0),
                      child: Text(
                        'A busca é feita pelo código FUNC, não pelo nome (evita confundir homônimos). Ex.: 142 ou FUNC-0142.',
                        style: TextStyle(fontSize: 13, color: AppColors.aviso),
                      ),
                    ),
                  const SizedBox(height: 20),
                  _Tabela(
                    funcionarios: visiveis,
                    cpfVisivel: _cpfVisivel,
                    destaque: widget.destaque,
                    onCpf: _mostrarCpf,
                    onFicha: _verFicha,
                    onAlterarSituacao: _alterarSituacao,
                  ),
                  Container(
                    padding: const EdgeInsets.fromLTRB(24, 8, 16, 8),
                    decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            filtrados.isEmpty
                                ? 'Nenhum resultado'
                                : 'Mostrando ${inicio + 1}–${inicio + visiveis.length} de ${filtrados.length}',
                            style: const TextStyle(fontSize: 14, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                          ),
                        ),
                        IconButton(
                          tooltip: 'Página anterior',
                          onPressed: pagina > 0 ? () => setState(() => _pagina = pagina - 1) : null,
                          icon: const Icon(Icons.chevron_left_rounded),
                        ),
                        Text(
                          '${pagina + 1} / $paginas',
                          style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
                        ),
                        IconButton(
                          tooltip: 'Próxima página',
                          onPressed: pagina < paginas - 1 ? () => setState(() => _pagina = pagina + 1) : null,
                          icon: const Icon(Icons.chevron_right_rounded),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

class _Filtros extends StatelessWidget {
  const _Filtros({
    required this.busca,
    required this.setores,
    required this.setor,
    required this.situacao,
    required this.onSetor,
    required this.onSituacao,
  });

  final TextEditingController busca;
  final List<String> setores;
  final String? setor;
  final _FiltroSituacao situacao;
  final ValueChanged<String?> onSetor;
  final ValueChanged<_FiltroSituacao> onSituacao;

  @override
  Widget build(BuildContext context) {
    final campoBusca = TextField(
      key: const Key('busca_funcionarios'),
      controller: busca,
      style: const TextStyle(fontSize: 14, color: AppColors.texto),
      textInputAction: TextInputAction.search,
      decoration: InputDecoration(
        isDense: true,
        hintText: 'Código FUNC (ex.: 142 ou FUNC-0142)',
        prefixIcon: const Icon(Icons.search_rounded, size: 20),
        prefixIconConstraints: const BoxConstraints(minWidth: 40, minHeight: 40),
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        fillColor: AppColors.superficie,
        suffixIcon: ValueListenableBuilder<TextEditingValue>(
          valueListenable: busca,
          builder: (context, valor, _) => valor.text.isEmpty
              ? const SizedBox.shrink()
              : IconButton(onPressed: busca.clear, tooltip: 'Limpar busca', icon: const Icon(Icons.close_rounded, size: 18)),
        ),
      ),
    );

    final campoSetor = DropdownButtonFormField<String?>(
      key: const Key('filtro_setor'),
      initialValue: setor,
      isExpanded: true,
      onChanged: onSetor,
      borderRadius: BorderRadius.circular(AppTheme.raioCampo),
      style: const TextStyle(fontSize: 14, color: AppColors.texto),
      decoration: const InputDecoration(
        isDense: true,
        prefixIcon: Icon(Icons.apartment_rounded, size: 20),
        prefixIconConstraints: BoxConstraints(minWidth: 40, minHeight: 40),
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        fillColor: AppColors.superficie,
      ),
      items: [
        const DropdownMenuItem<String?>(value: null, child: Text('Todos os setores')),
        for (final s in setores) DropdownMenuItem<String?>(value: s, child: Text(s)),
      ],
    );

    final situacoes = Wrap(
      spacing: 8,
      children: [
        for (final s in _FiltroSituacao.values)
          ChoiceChip(
            label: Text(s.rotulo),
            selected: s == situacao,
            onSelected: (_) => onSituacao(s),
            showCheckmark: false,
            selectedColor: AppColors.infoFundo,
            backgroundColor: AppColors.superficie,
            side: BorderSide(color: s == situacao ? AppColors.primaria : AppColors.borda),
            labelStyle: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: s == situacao ? AppColors.primaria : AppColors.textoSuave,
            ),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
          ),
      ],
    );

    return LayoutBuilder(
      builder: (context, limites) {
        if (limites.maxWidth < 900) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [campoBusca, const SizedBox(height: 12), campoSetor, const SizedBox(height: 12), situacoes],
          );
        }
        return Row(
          children: [
            Expanded(flex: 5, child: campoBusca),
            const SizedBox(width: 12),
            Expanded(flex: 3, child: campoSetor),
            const SizedBox(width: 16),
            situacoes,
          ],
        );
      },
    );
  }
}

class _Tabela extends StatelessWidget {
  const _Tabela({
    required this.funcionarios,
    required this.cpfVisivel,
    required this.destaque,
    required this.onCpf,
    required this.onFicha,
    required this.onAlterarSituacao,
  });

  final List<Funcionario> funcionarios;
  final Set<int> cpfVisivel;
  final String? destaque;
  final ValueChanged<Funcionario> onCpf;
  final ValueChanged<Funcionario> onFicha;
  final ValueChanged<Funcionario> onAlterarSituacao;

  static const _colunas = [
    ColunaTabela('Código', flex: 11),
    ColunaTabela('Funcionário', flex: 26),
    ColunaTabela('Setor', flex: 13),
    ColunaTabela('Perfil', flex: 10),
    ColunaTabela('CPF', flex: 17),
    ColunaTabela('Admissão', flex: 11),
    ColunaTabela('Situação', flex: 10),
    ColunaTabela('', flex: 6, alinhamento: Alignment.centerRight),
  ];

  @override
  Widget build(BuildContext context) {
    return TabelaAdmin(
      colunas: _colunas,
      larguraMinima: 1040,
      mensagemVazia: 'Nenhum funcionário encontrado com estes filtros.',
      linhas: [
        for (final f in funcionarios)
          [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Flexible(
                  child: Text(
                    f.codigo,
                    style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.primaria, fontFeatures: numerosTabulares),
                  ),
                ),
                if (f.codigo == destaque) ...[
                  const SizedBox(width: 6),
                  const EtiquetaStatus(texto: 'Novo', tom: TomStatus.sucesso, ponto: false),
                ],
              ],
            ),
            Row(
              children: [
                AvatarIniciais(iniciais: f.resumo.iniciais),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(f.nome, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto)),
                      const SizedBox(height: 2),
                      Text(f.cargo.nome, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12, color: AppColors.textoSuave)),
                    ],
                  ),
                ),
              ],
            ),
            Text(f.setor, style: const TextStyle(fontSize: 14, color: AppColors.texto)),
            Text(f.nivel.rotulo, style: const TextStyle(fontSize: 14, color: AppColors.texto)),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Flexible(
                  child: Text(
                    cpfVisivel.contains(f.id) ? Formatadores.cpf(f.cpf) : Formatadores.cpfMascarado(f.cpf),
                    style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
                  ),
                ),
                IconButton(
                  onPressed: () => onCpf(f),
                  tooltip: cpfVisivel.contains(f.id) ? 'Ocultar CPF de ${f.nome}' : 'Mostrar CPF de ${f.nome}',
                  visualDensity: VisualDensity.compact,
                  icon: Icon(
                    cpfVisivel.contains(f.id) ? Icons.visibility_off_rounded : Icons.visibility_rounded,
                    size: 18,
                    color: AppColors.textoSuave,
                  ),
                ),
              ],
            ),
            Text(
              Formatadores.data(f.dataAdmissao),
              style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
            ),
            EtiquetaStatus(texto: f.ativo ? 'Ativo' : 'Inativo', tom: f.ativo ? TomStatus.sucesso : TomStatus.neutro),
            PopupMenuButton<String>(
              tooltip: 'Ações para ${f.nome}',
              icon: const Icon(Icons.more_horiz_rounded, color: AppColors.textoSuave),
              onSelected: (acao) => acao == 'ficha' ? onFicha(f) : onAlterarSituacao(f),
              itemBuilder: (_) => [
                const PopupMenuItem(value: 'ficha', child: Text('Ver dados completos')),
                PopupMenuItem(value: 'situacao', child: Text(f.ativo ? 'Inativar' : 'Reativar')),
              ],
            ),
          ],
      ],
    );
  }
}

class _AvisoSucesso extends StatelessWidget {
  const _AvisoSucesso({required this.texto});

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.sucessoFundo,
        borderRadius: BorderRadius.circular(AppTheme.raioCampo),
      ),
      child: Row(
        children: [
          const Icon(Icons.check_circle_rounded, size: 20, color: AppColors.sucesso),
          const SizedBox(width: 10),
          Expanded(child: Text(texto, style: const TextStyle(fontSize: 14, color: AppColors.sucesso))),
        ],
      ),
    );
  }
}
