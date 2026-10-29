import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/validation/validadores.dart';
import '../../../domain/entities/estrutura_empresa.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../viewmodels/funcionarios_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Configurações: preferências da conta, níveis de acesso, estrutura da empresa (RF06),
/// registro de consultas a dados pessoais (RF22) e o canal com o RH (melhoria futura).
class AbaConfiguracoes extends ConsumerStatefulWidget {
  const AbaConfiguracoes({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  ConsumerState<AbaConfiguracoes> createState() => _AbaConfiguracoesState();
}

class _AbaConfiguracoesState extends ConsumerState<AbaConfiguracoes> {
  bool _alertaHorasExtras = true;
  bool _resumoSemanal = true;
  bool _alertaClima = true;

  Future<void> _adicionarCargo() async {
    final cargo = await showDialog<Cargo>(context: context, builder: (_) => const _DialogoCargo());
    if (cargo != null && mounted) avisarAdmin(context, 'Cargo "${cargo.nome}" adicionado.');
  }

  Future<void> _adicionarSetor() async {
    final setor = await showDialog<Setor>(context: context, builder: (_) => const _DialogoSetor());
    if (setor != null && mounted) avisarAdmin(context, 'Setor "${setor.nome}" adicionado.');
  }

  @override
  Widget build(BuildContext context) {
    final estrutura = ref.watch(estruturaViewModelProvider);
    final funcionarios = ref.watch(funcionariosViewModelProvider).valueOrNull ?? const [];
    final consultas = ref.watch(consultasDadosPessoaisProvider);
    final u = widget.usuario;

    return PaginaAdmin(
      titulo: 'Configurações',
      subtitulo: 'Preferências da conta, estrutura da empresa e privacidade',
      children: [
        DuasColunas(
          flexEsquerda: 1,
          flexDireita: 1,
          esquerda: PainelAdmin(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                CabecalhoPainel(
                  titulo: 'Minha conta',
                  subtitulo: '${u.nome} · ${u.codigo} · ${u.email}',
                  acao: BotaoAdmin(
                    rotulo: 'Alterar senha',
                    icone: Icons.lock_reset_rounded,
                    compacto: true,
                    onPressed: () => avisarAdmin(context, 'Enviamos um código de redefinição para ${u.email}.'),
                  ),
                ),
                const SizedBox(height: 8),
                _Preferencia(
                  titulo: 'Alertas de horas extras',
                  descricao: 'E-mail quando alguém passa de 2 h extras no dia (RN01).',
                  valor: _alertaHorasExtras,
                  onChanged: (v) => setState(() => _alertaHorasExtras = v),
                ),
                _Preferencia(
                  titulo: 'Alerta confidencial de clima',
                  descricao: '3 dias seguidos de "Estressado" no termômetro (RN08).',
                  valor: _alertaClima,
                  onChanged: (v) => setState(() => _alertaClima = v),
                ),
                _Preferencia(
                  titulo: 'Resumo semanal do dashboard',
                  descricao: 'Toda segunda-feira, às 8h.',
                  valor: _resumoSemanal,
                  onChanged: (v) => setState(() => _resumoSemanal = v),
                ),
                const Padding(
                  padding: EdgeInsets.fromLTRB(24, 8, 24, 24),
                  child: _CanalRh(),
                ),
              ],
            ),
          ),
          direita: _NiveisAcesso(
            contagem: {
              for (final n in NivelAcesso.values) n: funcionarios.where((f) => f.ativo && f.nivel == n).length,
            },
          ),
        ),
        const SizedBox(height: 24),
        estrutura.when(
          loading: () => const EstadoCarregando(texto: 'Carregando a estrutura da empresa…'),
          error: (_, __) => EstadoErro(
            titulo: 'Não foi possível carregar cargos e setores',
            onTentar: () => ref.invalidate(estruturaViewModelProvider),
          ),
          data: (e) => DuasColunas(
            flexEsquerda: 1,
            flexDireita: 1,
            esquerda: _Cargos(cargos: e.cargos, onAdicionar: _adicionarCargo),
            direita: _Setores(
              setores: e.setores,
              colaboradores: {
                for (final s in e.setores) s.nome: funcionarios.where((f) => f.ativo && f.setor == s.nome).length,
              },
              onAdicionar: _adicionarSetor,
            ),
          ),
        ),
        const SizedBox(height: 24),
        PainelAdmin(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const CabecalhoPainel(
                titulo: 'Privacidade (LGPD)',
                subtitulo: 'Quem viu dados pessoais completos (CPF, RG, endereço), conforme a Lei 13.709/2018',
              ),
              const SizedBox(height: 12),
              ...consultas.when(
                loading: () => [const Padding(padding: EdgeInsets.all(24), child: LinearProgressIndicator())],
                error: (_, __) => [
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Não foi possível carregar o registro.', style: TextStyle(color: AppColors.erro)),
                  ),
                ],
                data: (lista) => lista.isEmpty
                    ? [
                        const Padding(
                          padding: EdgeInsets.fromLTRB(24, 8, 24, 24),
                          child: Text(
                            'Nenhuma consulta registrada nesta sessão. Ao mostrar um CPF em Funcionários, o acesso aparece aqui.',
                            style: TextStyle(fontSize: 14, color: AppColors.textoSuave),
                          ),
                        ),
                      ]
                    : [
                        for (final c in lista)
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
                            child: Row(
                              children: [
                                const Icon(Icons.visibility_rounded, size: 18, color: AppColors.destaque),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    'CPF de ${c.funcionario.nome} (${c.funcionario.matricula}) visto por ${c.consultadoPor}',
                                    style: const TextStyle(fontSize: 14, color: AppColors.texto),
                                  ),
                                ),
                                Text(
                                  '${Formatadores.data(c.momento)} ${Formatadores.hora(c.momento)}',
                                  style: const TextStyle(fontSize: 12, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                                ),
                              ],
                            ),
                          ),
                        const SizedBox(height: 8),
                      ],
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _Preferencia extends StatelessWidget {
  const _Preferencia({required this.titulo, required this.descricao, required this.valor, required this.onChanged});

  final String titulo;
  final String descricao;
  final bool valor;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return SwitchListTile(
      value: valor,
      onChanged: onChanged,
      activeThumbColor: AppColors.primaria,
      contentPadding: const EdgeInsets.symmetric(horizontal: 24),
      title: Text(titulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
      subtitle: Text(descricao, style: const TextStyle(fontSize: 13, color: AppColors.textoSuave)),
    );
  }
}

class _CanalRh extends StatelessWidget {
  const _CanalRh();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.superficieSuave,
        borderRadius: BorderRadius.circular(AppTheme.raioCampo),
        border: Border.all(color: AppColors.borda),
      ),
      child: const Row(
        children: [
          Icon(Icons.forum_rounded, size: 22, color: AppColors.textoSuave),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Canal de conversa com o RH', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                SizedBox(height: 2),
                Text(
                  'Melhoria futura: o chat ainda não tem tabela no banco RHSystem.',
                  style: TextStyle(fontSize: 13, color: AppColors.textoSuave),
                ),
              ],
            ),
          ),
          EtiquetaStatus(texto: 'Em breve', tom: TomStatus.neutro, ponto: false),
        ],
      ),
    );
  }
}

class _NiveisAcesso extends StatelessWidget {
  const _NiveisAcesso({required this.contagem});

  final Map<NivelAcesso, int> contagem;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(
            titulo: 'Níveis de acesso',
            subtitulo: 'O perfil de cada funcionário é definido no cadastro',
          ),
          const SizedBox(height: 12),
          for (final n in NivelAcesso.values)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    decoration: BoxDecoration(color: AppColors.infoFundo, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
                    child: Icon(
                      switch (n) {
                        NivelAcesso.colaborador => Icons.person_rounded,
                        NivelAcesso.gestor => Icons.supervisor_account_rounded,
                        NivelAcesso.rh => Icons.badge_rounded,
                        NivelAcesso.admin => Icons.admin_panel_settings_rounded,
                      },
                      size: 18,
                      color: AppColors.primaria,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(n.rotulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                        const SizedBox(height: 2),
                        Text(n.descricao, style: const TextStyle(fontSize: 13, height: 1.4, color: AppColors.textoSuave)),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Contador(valor: contagem[n] ?? 0),
                ],
              ),
            ),
          const SizedBox(height: 8),
        ],
      ),
    );
  }
}

class _Cargos extends StatelessWidget {
  const _Cargos({required this.cargos, required this.onAdicionar});

  final List<Cargo> cargos;
  final VoidCallback onAdicionar;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Cargos',
            subtitulo: 'A carga horária do cargo é a base do cálculo de horas (RF11)',
            contagem: cargos.length,
            acao: BotaoAdmin(rotulo: 'Adicionar cargo', icone: Icons.add_rounded, compacto: true, onPressed: onAdicionar),
          ),
          const SizedBox(height: 20),
          TabelaAdmin(
            colunas: const [
              ColunaTabela('Cargo', flex: 5),
              ColunaTabela('Semanal', flex: 2),
              ColunaTabela('Por dia', flex: 2, alinhamento: Alignment.centerRight),
            ],
            larguraMinima: 420,
            mensagemVazia: 'Nenhum cargo cadastrado.',
            linhas: [
              for (final c in cargos)
                [
                  Text(c.nome, style: const TextStyle(fontSize: 14, color: AppColors.texto)),
                  Text(Formatadores.horas(c.cargaHorariaSemanal),
                      style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares)),
                  Text(Formatadores.horas(c.cargaHorariaDiaria),
                      style: const TextStyle(fontSize: 14, color: AppColors.textoSuave, fontFeatures: numerosTabulares)),
                ],
            ],
          ),
        ],
      ),
    );
  }
}

class _Setores extends StatelessWidget {
  const _Setores({required this.setores, required this.colaboradores, required this.onAdicionar});

  final List<Setor> setores;
  final Map<String, int> colaboradores;
  final VoidCallback onAdicionar;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CabecalhoPainel(
            titulo: 'Setores e gestores',
            subtitulo: 'O gestor aprova os ajustes de ponto e registra advertências do setor',
            contagem: setores.length,
            acao: BotaoAdmin(rotulo: 'Adicionar setor', icone: Icons.add_rounded, compacto: true, onPressed: onAdicionar),
          ),
          const SizedBox(height: 20),
          TabelaAdmin(
            colunas: const [
              ColunaTabela('Setor', flex: 4),
              ColunaTabela('Gestor', flex: 4),
              ColunaTabela('Ativos', flex: 2, alinhamento: Alignment.centerRight),
            ],
            larguraMinima: 420,
            mensagemVazia: 'Nenhum setor cadastrado.',
            linhas: [
              for (final s in setores)
                [
                  Text(s.nome, style: const TextStyle(fontSize: 14, color: AppColors.texto)),
                  Text(s.gestor ?? 'Sem gestor', style: TextStyle(fontSize: 14, color: s.gestor == null ? AppColors.textoSuave : AppColors.texto)),
                  Text('${colaboradores[s.nome] ?? 0}',
                      style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares)),
                ],
            ],
          ),
        ],
      ),
    );
  }
}

class _DialogoCargo extends ConsumerStatefulWidget {
  const _DialogoCargo();

  @override
  ConsumerState<_DialogoCargo> createState() => _DialogoCargoState();
}

class _DialogoCargoState extends ConsumerState<_DialogoCargo> {
  final _form = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _carga = TextEditingController(text: '44');
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _nome.dispose();
    _carga.dispose();
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _enviando = true);
    final cargo = Cargo(nome: _nome.text.trim(), cargaHorariaSemanal: double.parse(_carga.text.replaceAll(',', '.')));
    try {
      await ref.read(estruturaViewModelProvider.notifier).adicionarCargo(cargo);
      if (mounted) Navigator.of(context).pop(cargo);
    } on Falha catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DialogoAdmin(
      titulo: 'Adicionar cargo',
      rotuloConfirmar: 'Adicionar',
      enviando: _enviando,
      onConfirmar: _salvar,
      conteudo: Form(
        key: _form,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_erro != null) ...[Text(_erro!, style: const TextStyle(color: AppColors.erro)), const SizedBox(height: 12)],
            CampoAdmin(
              rotulo: 'Nome do cargo',
              controller: _nome,
              textCapitalization: TextCapitalization.sentences,
              validator: (v) => Validadores.obrigatorio(v, 'Informe o nome do cargo.'),
            ),
            const SizedBox(height: 16),
            CampoAdmin(
              rotulo: 'Carga horária semanal (h)',
              controller: _carga,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              validator: (v) {
                final n = double.tryParse((v ?? '').replaceAll(',', '.'));
                if (n == null) return 'Informe as horas semanais.';
                if (n <= 0 || n > 44) return 'Entre 1 e 44 horas (limite da CLT).';
                return null;
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _DialogoSetor extends ConsumerStatefulWidget {
  const _DialogoSetor();

  @override
  ConsumerState<_DialogoSetor> createState() => _DialogoSetorState();
}

class _DialogoSetorState extends ConsumerState<_DialogoSetor> {
  final _form = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _gestor = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _nome.dispose();
    _gestor.dispose();
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _enviando = true);
    final gestor = _gestor.text.trim();
    final setor = Setor(nome: _nome.text.trim(), gestor: gestor.isEmpty ? null : gestor);
    try {
      await ref.read(estruturaViewModelProvider.notifier).adicionarSetor(setor);
      if (mounted) Navigator.of(context).pop(setor);
    } on Falha catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DialogoAdmin(
      titulo: 'Adicionar setor',
      rotuloConfirmar: 'Adicionar',
      enviando: _enviando,
      onConfirmar: _salvar,
      conteudo: Form(
        key: _form,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_erro != null) ...[Text(_erro!, style: const TextStyle(color: AppColors.erro)), const SizedBox(height: 12)],
            CampoAdmin(
              rotulo: 'Nome do setor',
              controller: _nome,
              textCapitalization: TextCapitalization.words,
              validator: (v) => Validadores.obrigatorio(v, 'Informe o nome do setor.'),
            ),
            const SizedBox(height: 16),
            CampoAdmin(
              rotulo: 'Gestor responsável',
              controller: _gestor,
              dica: 'Opcional',
              textCapitalization: TextCapitalization.words,
            ),
          ],
        ),
      ),
    );
  }
}
