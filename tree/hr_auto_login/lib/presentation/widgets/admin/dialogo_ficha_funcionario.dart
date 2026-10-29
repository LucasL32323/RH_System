import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/funcionario.dart';
import 'componentes_admin.dart';

/// Ficha completa do funcionário (RF04, RF22): identificação e dados pessoais (CPF, RG, nascimento,
/// telefone e residência). Quem abre este diálogo já passou por `consultarFicha`, que audita a consulta.
class DialogoFichaFuncionario extends StatelessWidget {
  const DialogoFichaFuncionario({super.key, required this.funcionario});

  final Funcionario funcionario;

  static const String _vazio = '—';

  /// "13991234567" -> "(13) 99123-4567"; sem 10 ou 11 dígitos, devolve o texto como veio.
  static String telefoneFormatado(String bruto) {
    final d = Formatadores.digitos(bruto);
    if (d.length == 11) return '(${d.substring(0, 2)}) ${d.substring(2, 7)}-${d.substring(7)}';
    if (d.length == 10) return '(${d.substring(0, 2)}) ${d.substring(2, 6)}-${d.substring(6)}';
    return bruto.trim().isEmpty ? _vazio : bruto;
  }

  /// Duas ou três linhas: rua, bairro/cidade e CEP. "—" quando a ficha não trouxe endereço.
  static String enderecoFormatado(Endereco e) {
    if (e.logradouro.trim().isEmpty) return _vazio;
    final rua = StringBuffer('${e.logradouro}, ${e.numero}');
    if (e.complemento.isNotEmpty) rua.write(' - ${e.complemento}');
    final cep = e.cep.isEmpty ? '' : '\nCEP ${Formatadores.cep(e.cep)}';
    return '$rua\n${e.bairro} · ${e.cidade}/${e.uf}$cep';
  }

  static String _ouVazio(String texto) => texto.trim().isEmpty ? _vazio : texto;

  @override
  Widget build(BuildContext context) {
    final f = funcionario;
    // A lista da API vem sem nascimento: o repositório usa 01/01/1900 como "sem valor".
    final nascimento = f.dataNascimento.year <= 1900 ? _vazio : Formatadores.data(f.dataNascimento);

    return Dialog(
      key: const Key('dialogo_ficha'),
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
              Row(
                children: [
                  AvatarIniciais(iniciais: f.resumo.iniciais, tamanho: 48),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          f.nome,
                          style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.texto),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${f.codigo} · ${_ouVazio(f.cargo.nome)}',
                          style: const TextStyle(fontSize: 14, color: AppColors.textoSuave, fontFeatures: numerosTabulares),
                        ),
                      ],
                    ),
                  ),
                  EtiquetaStatus(texto: f.ativo ? 'Ativo' : 'Inativo', tom: f.ativo ? TomStatus.sucesso : TomStatus.neutro),
                ],
              ),
              const SizedBox(height: 20),
              _Secao(titulo: 'Na empresa', linhas: [
                _Linha('Setor', _ouVazio(f.setor)),
                _Linha('Gestor', _ouVazio(f.gestor ?? '')),
                _Linha('Perfil', f.nivel.rotulo),
                _Linha('Admissão', Formatadores.data(f.dataAdmissao)),
                _Linha('E-mail', _ouVazio(f.email)),
              ]),
              const SizedBox(height: 16),
              _Secao(titulo: 'Dados pessoais', linhas: [
                _Linha('CPF', f.cpf.isEmpty ? _vazio : Formatadores.cpf(f.cpf)),
                _Linha('RG', _ouVazio(f.rg)),
                _Linha('Nascimento', nascimento),
                _Linha('Telefone', telefoneFormatado(f.telefone)),
                _Linha('Residência', enderecoFormatado(f.endereco)),
              ]),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                decoration: BoxDecoration(
                  color: AppColors.infoFundo,
                  borderRadius: BorderRadius.circular(AppTheme.raioCampo),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.lock_outline_rounded, size: 18, color: AppColors.primaria),
                    SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'Esta consulta foi registrada no log de privacidade (LGPD).',
                        style: TextStyle(fontSize: 13, color: AppColors.primaria),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Align(
                alignment: Alignment.centerRight,
                child: BotaoAdmin(
                  rotulo: 'Fechar',
                  variante: VarianteBotao.primario,
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Secao extends StatelessWidget {
  const _Secao({required this.titulo, required this.linhas});

  final String titulo;
  final List<_Linha> linhas;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          titulo.toUpperCase(),
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 0.6, color: AppColors.textoSuave),
        ),
        const SizedBox(height: 8),
        for (final l in linhas)
          Container(
            padding: const EdgeInsets.symmetric(vertical: 10),
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 112,
                  child: Text(l.rotulo, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave)),
                ),
                Expanded(
                  child: Text(
                    l.valor,
                    style: const TextStyle(fontSize: 14, color: AppColors.texto, fontFeatures: numerosTabulares),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

class _Linha {
  const _Linha(this.rotulo, this.valor);

  final String rotulo;
  final String valor;
}
