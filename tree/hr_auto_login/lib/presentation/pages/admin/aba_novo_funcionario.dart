import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/errors/falha.dart';
import '../../../core/formatting/formatadores.dart';
import '../../../core/formatting/mascara_entrada.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/validation/validadores.dart';
import '../../../domain/entities/estrutura_empresa.dart';
import '../../../domain/entities/funcionario.dart';
import '../../viewmodels/funcionarios_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Novo funcionário (RF04, RF05, RF06): identificação, acesso, vínculo (perfil, setor, jornada)
/// e residência. O código FUNC é gerado pelo sistema e não pode ser alterado (RN10).
class AbaNovoFuncionario extends ConsumerStatefulWidget {
  const AbaNovoFuncionario({super.key, required this.onCadastrado, required this.onCancelar});

  final ValueChanged<String> onCadastrado;
  final VoidCallback onCancelar;

  @override
  ConsumerState<AbaNovoFuncionario> createState() => _AbaNovoFuncionarioState();
}

class _AbaNovoFuncionarioState extends ConsumerState<AbaNovoFuncionario> {
  final _form = GlobalKey<FormState>();

  final _nome = TextEditingController();
  final _cpf = TextEditingController();
  final _rg = TextEditingController();
  final _nascimento = TextEditingController();
  final _email = TextEditingController();
  final _telefone = TextEditingController();
  final _admissao = TextEditingController(text: Formatadores.data(DateTime.now()));
  final _cep = TextEditingController();
  final _logradouro = TextEditingController();
  final _numero = TextEditingController();
  final _complemento = TextEditingController();
  final _bairro = TextEditingController();
  final _cidade = TextEditingController();
  final _uf = TextEditingController();

  NivelAcesso _nivel = NivelAcesso.colaborador;
  Cargo? _cargo;
  Setor? _setor;
  bool _enviarConvite = true;
  bool _enviando = false;
  String? _erro;

  /// Depois da primeira tentativa, cada campo revalida ao ser editado (o erro some quando é corrigido).
  bool _tentouSalvar = false;

  @override
  void dispose() {
    for (final c in [
      _nome, _cpf, _rg, _nascimento, _email, _telefone, _admissao,
      _cep, _logradouro, _numero, _complemento, _bairro, _cidade, _uf,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _salvar() async {
    setState(() {
      _erro = null;
      _tentouSalvar = true;
    });
    if (!_form.currentState!.validate()) {
      setState(() => _erro = 'Revise os campos destacados.');
      return;
    }
    setState(() => _enviando = true);
    try {
      final novo = await ref.read(funcionariosViewModelProvider.notifier).cadastrar(NovoFuncionario(
            nome: Validadores.normalizarNome(_nome.text),
            cpf: Formatadores.digitos(_cpf.text),
            rg: _rg.text.trim(),
            dataNascimento: Formatadores.lerData(_nascimento.text)!,
            dataAdmissao: Formatadores.lerData(_admissao.text)!,
            email: _email.text.trim().toLowerCase(),
            telefone: _telefone.text.trim(),
            cargo: _cargo!,
            setor: _setor!,
            nivel: _nivel,
            enviarConvite: _enviarConvite,
            endereco: Endereco(
              cep: Formatadores.digitos(_cep.text),
              logradouro: _logradouro.text.trim(),
              numero: _numero.text.trim(),
              complemento: _complemento.text.trim(),
              bairro: _bairro.text.trim(),
              cidade: _cidade.text.trim(),
              uf: _uf.text.trim().toUpperCase(),
            ),
          ));
      if (!mounted) return;
      avisarAdmin(
        context,
        _enviarConvite
            ? '${novo.codigo} cadastrado. Convite de acesso enviado para ${novo.email}.'
            : '${novo.codigo} cadastrado.',
      );
      widget.onCadastrado(novo.codigo);
    } on Falha catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    } catch (_) {
      if (mounted) setState(() => _erro = 'Não foi possível cadastrar. Tente novamente.');
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final codigo = ref.watch(proximoCodigoProvider).valueOrNull ?? '...';
    final estrutura = ref.watch(estruturaViewModelProvider).valueOrNull;
    final hoje = DateTime.now();

    return PaginaAdmin(
      titulo: 'Novo funcionário',
      subtitulo: 'Cadastro com identificação, acesso, vínculo e residência',
      acoes: [
        BotaoAdmin(rotulo: 'Cancelar', onPressed: _enviando ? null : widget.onCancelar),
        BotaoAdmin(
          rotulo: _enviando ? 'Salvando…' : 'Cadastrar funcionário',
          icone: Icons.check_rounded,
          variante: VarianteBotao.primario,
          onPressed: _enviando ? null : _salvar,
        ),
      ],
      children: [
        Form(
          key: _form,
          autovalidateMode: _tentouSalvar ? AutovalidateMode.onUserInteraction : AutovalidateMode.disabled,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _CodigoGerado(codigo: codigo),
              if (_erro != null) ...[
                const SizedBox(height: 16),
                Container(
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(color: AppColors.erroFundo, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
                  child: Row(
                    children: [
                      const Icon(Icons.error_outline_rounded, size: 20, color: AppColors.erro),
                      const SizedBox(width: 10),
                      Expanded(child: Text(_erro!, style: const TextStyle(fontSize: 14, color: AppColors.erro))),
                    ],
                  ),
                ),
              ],
              const SizedBox(height: 24),
              _Secao(
                numero: 1,
                titulo: 'Identificação',
                subtitulo: 'Dados pessoais. CPF e RG aparecem mascarados para quem não é Admin (RN09).',
                campos: [
                  CampoAdmin(
                    rotulo: 'Nome completo',
                    controller: _nome,
                    textCapitalization: TextCapitalization.words,
                    validator: Validadores.nomeCompleto,
                  ),
                  LinhaCampos(campos: [
                    CampoAdmin(rotulo: 'CPF', controller: _cpf, dica: '000.000.000-00', mascara: MascaraEntrada.cpf, validator: Validadores.cpf),
                    CampoAdmin(
                      rotulo: 'RG',
                      controller: _rg,
                      validator: (v) => Validadores.obrigatorio(v, 'Informe o RG.'),
                    ),
                    CampoAdmin(
                      rotulo: 'Data de nascimento',
                      controller: _nascimento,
                      dica: 'dd/mm/aaaa',
                      mascara: MascaraEntrada.data,
                      validator: (v) => Validadores.data(
                        v,
                        rotulo: 'data de nascimento',
                        minima: DateTime(1920),
                        maxima: DateTime(hoje.year - 14, hoje.month, hoje.day),
                      ),
                    ),
                  ]),
                  LinhaCampos(campos: [
                    CampoAdmin(
                      rotulo: 'E-mail corporativo',
                      controller: _email,
                      dica: 'nome.sobrenome@hrauto.local',
                      keyboardType: TextInputType.emailAddress,
                      validator: Validadores.email,
                    ),
                    CampoAdmin(
                      rotulo: 'Telefone',
                      controller: _telefone,
                      dica: '(13) 99999-9999',
                      mascara: MascaraEntrada.telefone,
                      validator: Validadores.telefone,
                    ),
                  ]),
                ],
              ),
              const SizedBox(height: 24),
              _Secao(
                numero: 2,
                titulo: 'Acesso',
                subtitulo: 'O perfil define as telas e os dados que a pessoa vê no app (RF03).',
                campos: [
                  SelecaoAdmin<NivelAcesso>(
                    rotulo: 'Perfil de acesso',
                    valor: _nivel,
                    opcoes: NivelAcesso.values,
                    rotuloDe: (n) => n.rotulo,
                    ajuda: _nivel.descricao,
                    onChanged: (n) => setState(() => _nivel = n ?? NivelAcesso.colaborador),
                  ),
                  SwitchListTile(
                    key: const Key('enviar_convite'),
                    value: _enviarConvite,
                    onChanged: (v) => setState(() => _enviarConvite = v),
                    contentPadding: EdgeInsets.zero,
                    activeThumbColor: AppColors.primaria,
                    title: const Text('Enviar convite de acesso por e-mail',
                        style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                    subtitle: const Text('A pessoa define a própria senha no primeiro acesso.',
                        style: TextStyle(fontSize: 13, color: AppColors.textoSuave)),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              _Secao(
                numero: 3,
                titulo: 'Vínculo',
                subtitulo: 'Cargo (com a carga horária usada no cálculo de horas), setor e data de admissão.',
                campos: [
                  LinhaCampos(campos: [
                    SelecaoAdmin<Cargo>(
                      rotulo: 'Cargo',
                      valor: _cargo,
                      opcoes: estrutura?.cargos ?? const [],
                      rotuloDe: (c) => c.nome,
                      ajuda: _cargo == null
                          ? null
                          : 'Jornada de ${Formatadores.horas(_cargo!.cargaHorariaSemanal)} semanais '
                              '(${Formatadores.horas(_cargo!.cargaHorariaDiaria)} por dia)',
                      onChanged: (c) => setState(() => _cargo = c),
                      validator: (c) => c == null ? 'Escolha o cargo.' : null,
                    ),
                    SelecaoAdmin<Setor>(
                      rotulo: 'Setor',
                      valor: _setor,
                      opcoes: estrutura?.setores ?? const [],
                      rotuloDe: (s) => s.nome,
                      ajuda: _setor?.gestor == null ? null : 'Gestor: ${_setor!.gestor}',
                      onChanged: (s) => setState(() => _setor = s),
                      validator: (s) => s == null ? 'Escolha o setor.' : null,
                    ),
                    CampoAdmin(
                      rotulo: 'Data de admissão',
                      controller: _admissao,
                      dica: 'dd/mm/aaaa',
                      mascara: MascaraEntrada.data,
                      validator: (v) => Validadores.data(
                        v,
                        rotulo: 'data de admissão',
                        minima: DateTime(1980),
                        maxima: hoje.add(const Duration(days: 90)),
                      ),
                    ),
                  ]),
                ],
              ),
              const SizedBox(height: 24),
              _Secao(
                numero: 4,
                titulo: 'Residência',
                subtitulo: 'Endereço de residência (dado pessoal protegido pela LGPD).',
                campos: [
                  LinhaCampos(flex: const [2, 5, 2], campos: [
                    CampoAdmin(rotulo: 'CEP', controller: _cep, dica: '00000-000', mascara: MascaraEntrada.cep, validator: Validadores.cep),
                    CampoAdmin(
                      rotulo: 'Logradouro',
                      controller: _logradouro,
                      textCapitalization: TextCapitalization.words,
                      validator: (v) => Validadores.obrigatorio(v, 'Informe o logradouro.'),
                    ),
                    CampoAdmin(
                      rotulo: 'Número',
                      controller: _numero,
                      validator: (v) => Validadores.obrigatorio(v, 'Informe o número.'),
                    ),
                  ]),
                  LinhaCampos(campos: [
                    CampoAdmin(rotulo: 'Complemento', controller: _complemento, dica: 'Opcional'),
                    CampoAdmin(
                      rotulo: 'Bairro',
                      controller: _bairro,
                      textCapitalization: TextCapitalization.words,
                      validator: (v) => Validadores.obrigatorio(v, 'Informe o bairro.'),
                    ),
                  ]),
                  LinhaCampos(flex: const [4, 1], campos: [
                    CampoAdmin(
                      rotulo: 'Cidade',
                      controller: _cidade,
                      textCapitalization: TextCapitalization.words,
                      validator: (v) => Validadores.obrigatorio(v, 'Informe a cidade.'),
                    ),
                    CampoAdmin(
                      rotulo: 'UF',
                      controller: _uf,
                      dica: 'SP',
                      textCapitalization: TextCapitalization.characters,
                      validator: Validadores.uf,
                    ),
                  ]),
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _CodigoGerado extends StatelessWidget {
  const _CodigoGerado({required this.codigo});

  final String codigo;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.infoFundo,
        borderRadius: BorderRadius.circular(AppTheme.raioCard),
        border: Border.all(color: AppColors.apoioTransl18),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(color: AppColors.primaria, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
            child: const Icon(Icons.tag_rounded, color: Colors.white, size: 22),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Código do funcionário', style: TextStyle(fontSize: 12, color: AppColors.textoSuave)),
                const SizedBox(height: 2),
                Text(
                  codigo,
                  key: const Key('codigo_gerado'),
                  style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.primaria, fontFeatures: numerosTabulares),
                ),
                const SizedBox(height: 2),
                const Text(
                  'Gerado em sequência pelo sistema. Não pode ser alterado depois do cadastro.',
                  style: TextStyle(fontSize: 12, color: AppColors.textoSuave),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Secao extends StatelessWidget {
  const _Secao({required this.numero, required this.titulo, required this.subtitulo, required this.campos});

  final int numero;
  final String titulo;
  final String subtitulo;
  final List<Widget> campos;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 28,
                height: 28,
                alignment: Alignment.center,
                decoration: const BoxDecoration(color: AppColors.infoFundo, shape: BoxShape.circle),
                child: Text('$numero', style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: AppColors.primaria)),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(titulo, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.texto)),
                    const SizedBox(height: 4),
                    Text(subtitulo, style: const TextStyle(fontSize: 13, height: 1.4, color: AppColors.textoSuave)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          for (var i = 0; i < campos.length; i++) ...[if (i > 0) const SizedBox(height: 16), campos[i]],
        ],
      ),
    );
  }
}
