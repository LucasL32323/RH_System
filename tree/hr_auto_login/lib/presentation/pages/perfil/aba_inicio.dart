import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/ponto.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../viewmodels/ponto_view_model.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/menu_icones_admin.dart' show iniciaisDoUsuario;
import '../../widgets/admin/pagina_admin.dart';
import '../../widgets/perfil/abas_perfil.dart';

/// Início das áreas Colaborador e RH: saudação, resumo do ponto de hoje e atalhos para as abas do perfil.
/// Os números do ponto vêm do mesmo [pontoViewModelProvider] da aba "Registro de ponto".
class AbaInicio extends ConsumerWidget {
  const AbaInicio({super.key, required this.usuario, required this.onAbrir});

  final UsuarioAutenticado usuario;
  final ValueChanged<AbaPerfil> onAbrir;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ponto = ref.watch(pontoViewModelProvider);
    final atalhos = [
      for (final aba in AbaPerfil.doPerfil(usuario.perfil))
        if (aba != AbaPerfil.inicio) aba,
    ];

    return PaginaAdmin(
      titulo: 'Olá, ${usuario.primeiroNome}!',
      subtitulo: Formatadores.dataPorExtenso(DateTime.now()),
      children: [
        _PainelBoasVindas(usuario: usuario),
        const SizedBox(height: 24),
        DuasColunas(
          flexEsquerda: 3,
          flexDireita: 2,
          larguraMinima: 900,
          esquerda: _PainelPontoHoje(
            estado: ponto,
            onAbrirPonto: () => onAbrir(AbaPerfil.ponto),
            onTentar: () => ref.invalidate(pontoViewModelProvider),
          ),
          direita: _PainelAtalhos(abas: atalhos, onAbrir: onAbrir),
        ),
      ],
    );
  }
}

class _PainelBoasVindas extends StatelessWidget {
  const _PainelBoasVindas({required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  Widget build(BuildContext context) {
    final rh = usuario.perfil == 'RH';
    return PainelAdmin(
      padding: const EdgeInsets.all(24),
      child: Row(
        children: [
          AvatarIniciais(
            iniciais: iniciaisDoUsuario(usuario.nome),
            tamanho: 56,
            fundo: AppColors.primaria,
            cor: Colors.white,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  usuario.nome,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.texto),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    EtiquetaStatus(texto: usuario.codigo, tom: TomStatus.neutro, ponto: false),
                    EtiquetaStatus(texto: usuario.perfil, tom: TomStatus.info, ponto: false),
                  ],
                ),
                const SizedBox(height: 10),
                Text(
                  rh
                      ? 'Você bate o ponto, baixa seus holerites e cuida do cadastro, das advertências e dos holerites da equipe.'
                      : 'Você bate o ponto, pede ajustes e baixa os seus holerites por aqui.',
                  style: const TextStyle(fontSize: 14, height: 1.4, color: AppColors.textoSuave),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PainelPontoHoje extends StatelessWidget {
  const _PainelPontoHoje({required this.estado, required this.onAbrirPonto, required this.onTentar});

  final AsyncValue<EspelhoPonto> estado;
  final VoidCallback onAbrirPonto;
  final VoidCallback onTentar;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(
            titulo: 'Seu ponto hoje',
            subtitulo: 'O horário da batida é o do servidor, no momento do toque.',
          ),
          const SizedBox(height: 16),
          ...estado.when(
            loading: () => const <Widget>[
              Padding(padding: EdgeInsets.fromLTRB(24, 8, 24, 24), child: LinearProgressIndicator()),
            ],
            error: (_, __) => <Widget>[
              const Padding(
                padding: EdgeInsets.fromLTRB(24, 0, 24, 12),
                child: Text(
                  'Não foi possível carregar o ponto. Verifique a conexão com o servidor.',
                  style: TextStyle(fontSize: 14, color: AppColors.erro),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: BotaoAdmin(rotulo: 'Tentar novamente', icone: Icons.refresh_rounded, onPressed: onTentar),
                ),
              ),
            ],
            data: (espelho) => _resumo(espelho),
          ),
        ],
      ),
    );
  }

  List<Widget> _resumo(EspelhoPonto espelho) {
    final proxima = espelho.proximaBatida;
    final saldo = espelho.saldoBancoHoras;
    final emAnalise = espelho.minhasSolicitacoes.where((a) => a.situacao == SituacaoAjuste.pendente).length;

    return [
      _LinhaInfo(
        rotulo: 'Próxima batida',
        valor: proxima == null ? 'Jornada de hoje encerrada' : proxima.rotulo,
      ),
      _LinhaInfo(
        rotulo: 'Trabalhado hoje',
        valor: Formatadores.duracao(espelho.hoje?.trabalhado ?? Duration.zero),
      ),
      _LinhaInfo(
        rotulo: 'Saldo do banco de horas',
        valor: Formatadores.duracao(saldo, sinal: true),
        cor: saldo.isNegative ? AppColors.erro : AppColors.sucesso,
      ),
      if (emAnalise > 0)
        _LinhaInfo(
          rotulo: 'Pedidos de ajuste em análise',
          valor: '$emAnalise',
        ),
      Padding(
        padding: const EdgeInsets.all(24),
        child: Align(
          alignment: Alignment.centerLeft,
          child: BotaoAdmin(
            key: const Key('inicio_abrir_ponto'),
            rotulo: 'Abrir registro de ponto',
            icone: Icons.fingerprint_rounded,
            variante: VarianteBotao.primario,
            onPressed: onAbrirPonto,
          ),
        ),
      ),
    ];
  }
}

class _LinhaInfo extends StatelessWidget {
  const _LinhaInfo({required this.rotulo, required this.valor, this.cor = AppColors.texto});

  final String rotulo;
  final String valor;
  final Color cor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
      decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
      child: Row(
        children: [
          Expanded(child: Text(rotulo, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave))),
          const SizedBox(width: 12),
          Flexible(
            child: Text(
              valor,
              textAlign: TextAlign.right,
              style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: cor, fontFeatures: numerosTabulares),
            ),
          ),
        ],
      ),
    );
  }
}

class _PainelAtalhos extends StatelessWidget {
  const _PainelAtalhos({required this.abas, required this.onAbrir});

  final List<AbaPerfil> abas;
  final ValueChanged<AbaPerfil> onAbrir;

  @override
  Widget build(BuildContext context) {
    return PainelAdmin(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CabecalhoPainel(titulo: 'Atalhos', subtitulo: 'O que o seu perfil pode abrir'),
          const SizedBox(height: 12),
          for (final aba in abas) _Atalho(aba: aba, onTap: () => onAbrir(aba)),
        ],
      ),
    );
  }
}

class _Atalho extends StatelessWidget {
  const _Atalho({required this.aba, required this.onTap});

  final AbaPerfil aba;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: aba.rotulo,
      excludeSemantics: true,
      child: InkWell(
        key: Key('atalho_${aba.name}'),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
          child: Row(
            children: [
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(color: AppColors.infoFundo, borderRadius: BorderRadius.circular(AppTheme.raioCampo)),
                child: Icon(aba.icone, size: 18, color: AppColors.primaria),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(aba.rotulo, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto)),
                    const SizedBox(height: 2),
                    Text(aba.descricao, style: const TextStyle(fontSize: 13, color: AppColors.textoSuave)),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right_rounded, color: AppColors.textoSuave),
            ],
          ),
        ),
      ),
    );
  }
}
