# Dia 2026-10-27 (sessão com o Lucas) — Fase 5 (parte 5): áreas do Colaborador e do RH

Código gerado em sessão interativa, pelo celular, **sem acesso ao PC do Lucas**. **NÃO compilado e NÃO testado**: o ambiente não tem Flutter/Dart e o proxy bloqueia o SDK e o pub.dev. Conferido só por leitura e pelo balanceamento de parênteses e chaves por script. Rode `flutter analyze` e `flutter test` no PC antes de confiar.

## O que entrou

Hoje só o Admin tinha área com menu; Colaborador e RH caíam numa tela provisória ("Login validado..."). Agora cada perfil tem a sua área, no mesmo modelo do Admin (menu lateral só com ícones, uma aba por vez, mesmo layout no celular e no computador — RNF08):

| Perfil | Abas do menu |
|---|---|
| Colaborador | Início, Registro de ponto, Holerites, Configurações |
| RH | as mesmas + Funcionários, Novo funcionário, Gestão de advertências |
| Admin | sem mudança (`AdminPage`, 7 abas + Holerites do pacote de 27/10) |

- **Início**: saudação pelo primeiro nome, código FUNC e perfil, resumo do ponto de hoje (próxima batida, trabalhado hoje, saldo do banco de horas, pedidos de ajuste em análise) vindo do mesmo `pontoViewModelProvider` e atalhos para as abas do perfil.
- **Registro de ponto**, **Holerites**, **Funcionários**, **Novo funcionário** e **Gestão de advertências** reaproveitam as abas que já existem (nada foi copiado): `AbaRegistroPonto`, `AbaHolerites`, `AbaFuncionarios`, `AbaNovoFuncionario`, `AbaAdvertencias`. As regras por perfil continuam no servidor; o menu só evita mostrar o que seria recusado.
- **Configurações** (Colaborador e RH): dados da conta, sessão válida até, texto de privacidade (LGPD), canal com o RH ("Em breve", sem tabela no banco) e botão "Sair da conta". Cargos, setores e níveis de acesso ficam só no Admin.
- `app.dart` passa a escolher `AreaPerfilPage` para `RH` e `Colaborador`; qualquer outro valor de perfil continua na `HomePage` provisória.

## Dependências (importante)

`AreaPerfilPage` importa `pages/admin/aba_holerites.dart`, que vem do pacote de **27/10 (`dia_2026-10-27_fase5.md`)**. Aplique antes 25/10, 26/10 e 27/10 (ou pelo menos o de 27/10, com os patches de pubspec, menu e admin_page). Sem ele, o projeto não compila.

## Como aplicar

1. Copie os 7 arquivos **novos** e substitua o `lib/app.dart` (mudou só o `switch` e um import).
2. `flutter pub get`, `flutter analyze`, `flutter test`.
3. Para ver: `flutter run -d chrome --dart-define=PROTOTIPO=false` com a API em http://localhost:5080. Entre com um usuário Colaborador ou RH (o admin de teste abre a área do Admin).

## Testes

`test/area_perfil_test.dart` (15 casos, todos com repositórios em memória e sessão simulada):

- `AbaPerfil.doPerfil` para Colaborador, RH e perfil desconhecido.
- Colaborador: 4 abas e nenhuma de gestão; Início com "Olá, Ana!", etiquetas e resumo; atalho e botão do Início abrem o ponto; Holerites só com "Meus holerites"; Configurações e saída pelos dois caminhos.
- RH: 7 abas; Funcionários, Novo funcionário e Advertências abrem; Holerites com consulta da equipe e publicação.
- Raiz: layout de desktop e perfil desconhecido na tela provisória.

O teste existente `auth_page_test.dart` ("cadastro válido ... permite entrar") continua válido: a conta cadastrada é Colaborador e o Início mostra "Olá, Ana!" e "Colaborador" uma vez cada (a barra superior usa "Área do colaborador" de propósito, para não duplicar o texto).

## Suposições e pontos para confirmar com o grupo

- O RH não vê o Dashboard da empresa (ele usa dados de exemplo e é da área do Admin); no lugar ele tem o Início com o resumo do próprio ponto. Se o grupo quiser o dashboard para o RH, é só incluir uma aba nova no enum `AbaPerfil`.
- "Gestor" (quem é `id_gestor` de outros) ainda não tem área própria: entra como Colaborador. Aprovar ajustes de ponto e férias da equipe fica para a próxima etapa.
- A troca de senha dentro do app não existe no Flutter; a tela de Configurações orienta a usar "Esqueci minha senha".
- Sessão ainda só em memória (armazenamento seguro do token segue pendente).


## Arquivos novos

### hr_auto_login/lib/presentation/widgets/perfil/abas_perfil.dart

```dart
import 'package:flutter/material.dart';

/// Abas da área de quem NÃO é Admin (perfis Colaborador e RH), na ordem do menu lateral.
/// O Admin continua com as 7 abas de `AbaAdmin` (menu_icones_admin.dart).
enum AbaPerfil {
  inicio('Início', 'Resumo do seu dia', Icons.home_rounded),
  ponto('Registro de ponto', 'Bater o ponto e ver o espelho', Icons.fingerprint_rounded),
  holerites('Holerites', 'Contracheques em PDF', Icons.receipt_long_rounded),
  funcionarios('Funcionários', 'Busca pelo código FUNC', Icons.groups_rounded),
  novoFuncionario('Novo funcionário', 'Cadastro de colaborador', Icons.person_add_alt_1_rounded),
  advertencias('Gestão de advertências', 'Semáforo e régua', Icons.gavel_rounded),
  configuracoes('Configurações', 'Conta, privacidade e canal com o RH', Icons.settings_rounded);

  const AbaPerfil(this.rotulo, this.descricao, this.icone);

  final String rotulo;
  final String descricao;
  final IconData icone;

  /// Abas que só o RH enxerga (o servidor também recusa quem não tem o perfil).
  static const Set<AbaPerfil> soRh = {funcionarios, novoFuncionario, advertencias};

  /// Abas liberadas para o [perfil] ('Colaborador' ou 'RH'); qualquer outro valor cai no menor conjunto.
  static List<AbaPerfil> doPerfil(String perfil) => [
        for (final aba in values)
          if (perfil == 'RH' || !soRh.contains(aba)) aba,
      ];
}
```

### hr_auto_login/lib/presentation/widgets/perfil/menu_icones_perfil.dart

```dart
import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../admin/componentes_admin.dart';
import '../admin/menu_icones_admin.dart' show iniciaisDoUsuario;
import 'abas_perfil.dart';

/// Menu lateral só com ícones (RNF08) das áreas Colaborador e RH: uma aba por vez,
/// mesmo layout no celular (trilho de 64 px) e no computador (80 px).
/// O nome de cada aba aparece no tooltip e no leitor de tela.
class MenuIconesPerfil extends StatelessWidget {
  const MenuIconesPerfil({
    super.key,
    required this.usuario,
    required this.abas,
    required this.abaAtiva,
    required this.onSelecionar,
    required this.onSair,
    this.compacto = false,
  });

  final UsuarioAutenticado usuario;
  final List<AbaPerfil> abas;
  final AbaPerfil abaAtiva;
  final ValueChanged<AbaPerfil> onSelecionar;
  final VoidCallback onSair;
  final bool compacto;

  static double larguraPara({required bool compacto}) => compacto ? 64 : 80;

  @override
  Widget build(BuildContext context) {
    final tamanhoItem = compacto ? 44.0 : 48.0;

    return Container(
      width: larguraPara(compacto: compacto),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [AppColors.primaria, AppColors.secundaria],
        ),
      ),
      child: SafeArea(
        right: false,
        child: Column(
          children: [
            SizedBox(
              height: 72,
              child: Center(
                child: Semantics(
                  label: 'HR-Auto',
                  excludeSemantics: true,
                  child: Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: AppColors.brancoTransl20,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: AppColors.brancoTransl20),
                    ),
                    child: const Icon(Icons.badge_rounded, color: Colors.white, size: 22),
                  ),
                ),
              ),
            ),
            Container(height: 1, margin: const EdgeInsets.symmetric(horizontal: 16), color: AppColors.brancoTransl12),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(vertical: 16),
                child: Column(
                  children: [
                    for (final aba in abas) ...[
                      // Configurações fica separada das demais, como no menu do Admin.
                      if (aba == AbaPerfil.configuracoes)
                        Container(
                          height: 1,
                          width: 32,
                          margin: const EdgeInsets.symmetric(vertical: 8),
                          color: AppColors.brancoTransl12,
                        ),
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: _ItemIcone(
                          aba: aba,
                          ativo: aba == abaAtiva,
                          tamanho: tamanhoItem,
                          onTap: () => onSelecionar(aba),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Column(
                children: [
                  Tooltip(
                    message: '${usuario.nome}\n${usuario.codigo} · ${usuario.perfil}',
                    child: AvatarIniciais(
                      iniciais: iniciaisDoUsuario(usuario.nome),
                      fundo: AppColors.brancoTransl20,
                      cor: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 8),
                  IconButton(
                    onPressed: onSair,
                    tooltip: 'Sair',
                    icon: const Icon(Icons.logout_rounded, size: 20, color: AppColors.brancoTransl80),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ItemIcone extends StatelessWidget {
  const _ItemIcone({required this.aba, required this.ativo, required this.tamanho, required this.onTap});

  final AbaPerfil aba;
  final bool ativo;
  final double tamanho;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: aba.rotulo,
      child: Semantics(
        label: aba.rotulo,
        selected: ativo,
        button: true,
        excludeSemantics: true,
        child: Material(
          color: ativo ? AppColors.brancoTransl20 : Colors.transparent,
          borderRadius: BorderRadius.circular(AppTheme.raioCampo),
          child: InkWell(
            key: Key('aba_${aba.name}'),
            onTap: onTap,
            borderRadius: BorderRadius.circular(AppTheme.raioCampo),
            hoverColor: AppColors.brancoTransl12,
            splashColor: AppColors.brancoTransl12,
            child: SizedBox(
              width: tamanho,
              height: tamanho,
              child: Stack(
                alignment: Alignment.center,
                children: [
                  if (ativo)
                    Positioned(
                      left: 0,
                      top: tamanho * 0.25,
                      bottom: tamanho * 0.25,
                      child: Container(
                        width: 3,
                        decoration: BoxDecoration(color: AppColors.apoio, borderRadius: BorderRadius.circular(2)),
                      ),
                    ),
                  Icon(aba.icone, size: 22, color: ativo ? Colors.white : AppColors.brancoTransl60),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
```

### hr_auto_login/lib/presentation/widgets/perfil/barra_superior_perfil.dart

```dart
import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../admin/componentes_admin.dart';
import '../admin/menu_icones_admin.dart' show iniciaisDoUsuario;
import 'abas_perfil.dart';

/// Barra superior (64 px) das áreas Colaborador e RH: trilha "Área › aba", data e usuário.
/// Não mostra o texto exato do perfil ("Colaborador"/"RH"): quem mostra a etiqueta é o Início.
class BarraSuperiorPerfil extends StatelessWidget {
  const BarraSuperiorPerfil({super.key, required this.usuario, required this.aba, required this.hoje});

  final UsuarioAutenticado usuario;
  final AbaPerfil aba;
  final DateTime hoje;

  String get _area => usuario.perfil == 'RH' ? 'Área do RH' : 'Área do colaborador';

  @override
  Widget build(BuildContext context) {
    final largura = MediaQuery.sizeOf(context).width;
    final completa = largura >= 760;

    return Container(
      height: 64,
      padding: EdgeInsets.symmetric(horizontal: completa ? 32 : 16),
      decoration: const BoxDecoration(
        color: AppColors.fundo,
        border: Border(bottom: BorderSide(color: AppColors.borda)),
      ),
      child: Row(
        children: [
          Expanded(
            child: Row(
              children: [
                if (completa) ...[
                  Text(_area, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave)),
                  const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 6),
                    child: Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.textoSuave),
                  ),
                ],
                Flexible(
                  child: Text(
                    aba.rotulo,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                  ),
                ),
              ],
            ),
          ),
          if (completa) ...[
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Text(
                Formatadores.dataPorExtenso(hoje),
                style: const TextStyle(fontSize: 14, color: AppColors.textoSuave),
              ),
            ),
            Container(width: 1, height: 24, margin: const EdgeInsets.symmetric(horizontal: 8), color: AppColors.borda),
            AvatarIniciais(
              iniciais: iniciaisDoUsuario(usuario.nome),
              tamanho: 32,
              fundo: AppColors.primaria,
              cor: Colors.white,
            ),
            const SizedBox(width: 8),
            Text(
              usuario.primeiroNome,
              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: AppColors.texto),
            ),
          ],
        ],
      ),
    );
  }
}
```

### hr_auto_login/lib/presentation/pages/perfil/aba_inicio.dart

```dart
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
```

### hr_auto_login/lib/presentation/pages/perfil/aba_configuracoes_perfil.dart

```dart
import 'package:flutter/material.dart';

import '../../../core/formatting/formatadores.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../widgets/admin/componentes_admin.dart';
import '../../widgets/admin/pagina_admin.dart';

/// Configurações de Colaborador e RH: dados da conta, privacidade (LGPD) e o canal com o RH.
/// Estrutura da empresa (cargos, setores) e níveis de acesso ficam só na área do Admin.
class AbaConfiguracoesPerfil extends StatelessWidget {
  const AbaConfiguracoesPerfil({super.key, required this.usuario, required this.onSair});

  final UsuarioAutenticado usuario;
  final VoidCallback onSair;

  @override
  Widget build(BuildContext context) {
    final expira = usuario.expiraEm.toLocal();

    return PaginaAdmin(
      titulo: 'Configurações',
      subtitulo: 'Dados da conta, privacidade e canal com o RH',
      children: [
        DuasColunas(
          flexEsquerda: 1,
          flexDireita: 1,
          larguraMinima: 900,
          esquerda: PainelAdmin(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const CabecalhoPainel(
                  titulo: 'Minha conta',
                  subtitulo: 'O código FUNC é gerado no cadastro e não muda.',
                ),
                const SizedBox(height: 12),
                _Dado(rotulo: 'Nome', valor: usuario.nome),
                _Dado(rotulo: 'Código', valor: usuario.codigo),
                _Dado(rotulo: 'E-mail', valor: usuario.email),
                _Dado(rotulo: 'Nível de acesso', valor: usuario.perfil),
                _Dado(rotulo: 'Sessão válida até', valor: '${Formatadores.data(expira)} ${Formatadores.hora(expira)}'),
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
                  child: const Text(
                    'A troca de senha dentro do app entra na próxima etapa. Por enquanto, use "Esqueci minha senha" na tela de login.',
                    style: TextStyle(fontSize: 13, height: 1.4, color: AppColors.textoSuave),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: BotaoAdmin(
                      key: const Key('configuracoes_sair'),
                      rotulo: 'Sair da conta',
                      icone: Icons.logout_rounded,
                      onPressed: onSair,
                    ),
                  ),
                ),
              ],
            ),
          ),
          direita: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const PainelAdmin(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    CabecalhoPainel(
                      titulo: 'Privacidade (LGPD)',
                      subtitulo: 'Lei 13.709/2018',
                    ),
                    Padding(
                      padding: EdgeInsets.fromLTRB(24, 16, 24, 24),
                      child: Text(
                        'CPF, RG e endereço só aparecem completos para o RH e o Admin, e cada consulta fica registrada com quem viu e quando. '
                        'Quando o RH baixa um holerite de outra pessoa, o acesso também é auditado.',
                        style: TextStyle(fontSize: 14, height: 1.5, color: AppColors.textoSuave),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              PainelAdmin(
                padding: const EdgeInsets.all(24),
                child: Container(
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
                            Text(
                              'Canal de conversa com o RH',
                              style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
                            ),
                            SizedBox(height: 2),
                            Text(
                              'Melhoria futura: o chat ainda não tem tabela no banco RHSystem.',
                              style: TextStyle(fontSize: 13, color: AppColors.textoSuave),
                            ),
                          ],
                        ),
                      ),
                      SizedBox(width: 8),
                      EtiquetaStatus(texto: 'Em breve', tom: TomStatus.neutro, ponto: false),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _Dado extends StatelessWidget {
  const _Dado({required this.rotulo, required this.valor});

  final String rotulo;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
      decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.borda))),
      child: Row(
        children: [
          Expanded(flex: 2, child: Text(rotulo, style: const TextStyle(fontSize: 14, color: AppColors.textoSuave))),
          const SizedBox(width: 12),
          Expanded(
            flex: 3,
            child: Text(
              valor,
              textAlign: TextAlign.right,
              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.texto),
            ),
          ),
        ],
      ),
    );
  }
}
```

### hr_auto_login/lib/presentation/pages/perfil/area_perfil_page.dart

```dart
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/usuario_autenticado.dart';
import '../../providers.dart';
import '../../widgets/perfil/abas_perfil.dart';
import '../../widgets/perfil/barra_superior_perfil.dart';
import '../../widgets/perfil/menu_icones_perfil.dart';
import '../admin/aba_advertencias.dart';
import '../admin/aba_funcionarios.dart';
import '../admin/aba_holerites.dart';
import '../admin/aba_novo_funcionario.dart';
import '../admin/aba_registro_ponto.dart';
import 'aba_configuracoes_perfil.dart';
import 'aba_inicio.dart';

/// Área dos perfis Colaborador e RH (o Admin tem a `AdminPage`).
/// Mesmo modelo do Admin: menu lateral só com ícones (RNF08), uma aba por vez e o mesmo layout
/// no celular e no computador.
///
///  - Colaborador: Início, Registro de ponto, Holerites e Configurações.
///  - RH: as mesmas, mais Funcionários, Novo funcionário e Gestão de advertências.
///
/// O que cada perfil pode fazer é decidido pelo servidor; este menu só evita mostrar o que seria recusado.
class AreaPerfilPage extends ConsumerStatefulWidget {
  const AreaPerfilPage({super.key, required this.usuario});

  final UsuarioAutenticado usuario;

  @override
  ConsumerState<AreaPerfilPage> createState() => _AreaPerfilPageState();
}

class _AreaPerfilPageState extends ConsumerState<AreaPerfilPage> {
  AbaPerfil _aba = AbaPerfil.inicio;

  /// Código do funcionário recém-cadastrado, destacado ao abrir a aba Funcionários.
  String? _destaque;

  late final List<AbaPerfil> _abas = AbaPerfil.doPerfil(widget.usuario.perfil);

  void _abrir(AbaPerfil aba, {String? destaque}) {
    // Atalhos só levam a abas liberadas para o perfil.
    if (!_abas.contains(aba)) return;
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    setState(() {
      _aba = aba;
      _destaque = destaque;
    });
  }

  void _sair() => ref.read(sessaoProvider.notifier).state = null;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final compacto = limites.maxWidth < 600;

        return Scaffold(
          backgroundColor: AppColors.fundo,
          body: Row(
            children: [
              MenuIconesPerfil(
                usuario: widget.usuario,
                abas: _abas,
                abaAtiva: _aba,
                compacto: compacto,
                onSelecionar: _abrir,
                onSair: _sair,
              ),
              Expanded(
                child: SafeArea(
                  left: false,
                  child: Column(
                    children: [
                      BarraSuperiorPerfil(usuario: widget.usuario, aba: _aba, hoje: DateTime.now()),
                      Expanded(
                        child: AnimatedSwitcher(
                          duration: const Duration(milliseconds: 200),
                          child: KeyedSubtree(key: ValueKey(_aba), child: _conteudo()),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _conteudo() => switch (_aba) {
        AbaPerfil.inicio => AbaInicio(usuario: widget.usuario, onAbrir: _abrir),
        AbaPerfil.ponto => AbaRegistroPonto(usuario: widget.usuario),
        AbaPerfil.holerites => AbaHolerites(usuario: widget.usuario),
        AbaPerfil.funcionarios => AbaFuncionarios(
            usuario: widget.usuario,
            destaque: _destaque,
            onNovo: () => _abrir(AbaPerfil.novoFuncionario),
          ),
        AbaPerfil.novoFuncionario => AbaNovoFuncionario(
            onCadastrado: (codigo) => _abrir(AbaPerfil.funcionarios, destaque: codigo),
            onCancelar: () => _abrir(AbaPerfil.funcionarios),
          ),
        AbaPerfil.advertencias => AbaAdvertencias(usuario: widget.usuario),
        AbaPerfil.configuracoes => AbaConfiguracoesPerfil(usuario: widget.usuario, onSair: _sair),
      };
}
```

### hr_auto_login/test/area_perfil_test.dart

```dart
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hr_auto/app.dart';
import 'package:hr_auto/data/repositories/admin_repository_prototipo.dart';
import 'package:hr_auto/data/repositories/holerite_repository_prototipo.dart';
import 'package:hr_auto/domain/entities/usuario_autenticado.dart';
import 'package:hr_auto/presentation/providers.dart';
import 'package:hr_auto/presentation/widgets/perfil/abas_perfil.dart';
import 'package:hr_auto/presentation/widgets/perfil/barra_superior_perfil.dart';

/// Áreas dos perfis Colaborador e RH: menu só com ícones, abas liberadas por perfil, Início,
/// navegação e saída. Repositórios em memória, sem rede (o app abre direto com a sessão simulada).
void main() {
  UsuarioAutenticado usuario(String perfil, {String nome = 'Ana Souza', String codigo = 'FUNC-0007'}) =>
      UsuarioAutenticado(
        token: 'token-de-teste',
        expiraEm: DateTime(2030),
        codigo: codigo,
        nome: nome,
        email: 'ana@hrauto.local',
        perfil: perfil,
      );

  Future<void> abrir(
    WidgetTester tester,
    UsuarioAutenticado u, {
    Size tamanho = const Size(420, 1400),
  }) async {
    tester.view.physicalSize = tamanho;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessaoProvider.overrideWith((ref) => u),
          adminRepositoryProvider.overrideWithValue(AdminRepositoryPrototipo(latencia: Duration.zero)),
          holeriteRepositoryProvider.overrideWithValue(
            HoleriteRepositoryPrototipo(
              codigoUsuario: u.codigo,
              nomeUsuario: u.nome,
              perfilUsuario: u.perfil,
              latencia: Duration.zero,
            ),
          ),
        ],
        child: const HrAutoApp(usarGoogleFonts: false),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> tocar(WidgetTester tester, Finder alvo) async {
    await tester.ensureVisible(alvo);
    await tester.pumpAndSettle();
    await tester.tap(alvo);
    await tester.pumpAndSettle();
  }

  Finder menu(AbaPerfil aba) => find.byKey(Key('aba_${aba.name}'));

  /// Título da aba atual na barra superior (o mesmo texto também aparece na página, por isso o filtro).
  Finder naBarra(String texto) =>
      find.descendant(of: find.byType(BarraSuperiorPerfil), matching: find.text(texto));

  group('AbaPerfil.doPerfil', () {
    test('Colaborador: Início, ponto, holerites e configurações', () {
      expect(AbaPerfil.doPerfil('Colaborador'), [
        AbaPerfil.inicio,
        AbaPerfil.ponto,
        AbaPerfil.holerites,
        AbaPerfil.configuracoes,
      ]);
    });

    test('RH ganha funcionários, novo funcionário e advertências', () {
      expect(AbaPerfil.doPerfil('RH'), AbaPerfil.values);
    });

    test('perfil desconhecido cai no menor conjunto', () {
      expect(AbaPerfil.doPerfil('Qualquer'), AbaPerfil.doPerfil('Colaborador'));
    });
  });

  group('Área do Colaborador', () {
    testWidgets('mostra só as 4 abas do perfil no menu de ícones', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      for (final aba in AbaPerfil.doPerfil('Colaborador')) {
        expect(find.byTooltip(aba.rotulo), findsOneWidget, reason: aba.rotulo);
      }
      expect(find.byTooltip('Funcionários'), findsNothing);
      expect(find.byTooltip('Novo funcionário'), findsNothing);
      expect(find.byTooltip('Gestão de advertências'), findsNothing);
      expect(find.byKey(const Key('atalho_funcionarios')), findsNothing);
    });

    testWidgets('Início saúda pelo primeiro nome e mostra o resumo do ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      expect(find.text('Olá, Ana!'), findsOneWidget);
      expect(find.text('Colaborador'), findsOneWidget);
      expect(find.text('FUNC-0007'), findsOneWidget);
      expect(find.text('Seu ponto hoje'), findsOneWidget);
      expect(find.text('Saldo do banco de horas'), findsOneWidget);
      expect(find.text('Atalhos'), findsOneWidget);
    });

    testWidgets('atalho do Início abre o Registro de ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byKey(const Key('atalho_ponto')));

      expect(naBarra('Registro de ponto'), findsOneWidget);
      expect(find.byKey(const Key('botao_bater_ponto')), findsOneWidget);
    });

    testWidgets('botão do resumo também abre o Registro de ponto', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byKey(const Key('inicio_abrir_ponto')));

      expect(find.byKey(const Key('botao_bater_ponto')), findsOneWidget);
    });

    testWidgets('Holerites: só "Meus holerites", sem consulta da equipe nem publicação', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, menu(AbaPerfil.holerites));

      expect(find.text('Meus holerites'), findsOneWidget);
      expect(find.text('Holerites da equipe'), findsNothing);
      expect(find.text('Publicar holerite'), findsNothing);
    });

    testWidgets('Configurações mostra a conta e o canal com o RH; "Sair da conta" volta ao login', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, menu(AbaPerfil.configuracoes));
      expect(find.text('Minha conta'), findsOneWidget);
      expect(find.text('ana@hrauto.local'), findsOneWidget);
      expect(find.text('Canal de conversa com o RH'), findsOneWidget);
      // Estrutura da empresa é só do Admin.
      expect(find.text('Cargos'), findsNothing);

      await tocar(tester, find.byKey(const Key('configuracoes_sair')));
      expect(find.text('Bem-vindo de volta'), findsOneWidget);
    });

    testWidgets('o ícone de sair do menu também encerra a sessão', (tester) async {
      await abrir(tester, usuario('Colaborador'));

      await tocar(tester, find.byTooltip('Sair'));

      expect(find.text('Bem-vindo de volta'), findsOneWidget);
    });
  });

  group('Área do RH', () {
    testWidgets('mostra as 7 abas, com as de gestão de pessoas', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      for (final aba in AbaPerfil.values) {
        expect(find.byTooltip(aba.rotulo), findsOneWidget, reason: aba.rotulo);
      }
      expect(find.text('Olá, Bruno!'), findsOneWidget);
      expect(find.text('RH'), findsOneWidget);
      expect(find.byKey(const Key('atalho_funcionarios')), findsOneWidget);
    });

    testWidgets('abre Funcionários, Novo funcionário e Gestão de advertências pelo menu', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      await tocar(tester, menu(AbaPerfil.funcionarios));
      expect(naBarra('Funcionários'), findsOneWidget);

      await tocar(tester, menu(AbaPerfil.novoFuncionario));
      expect(naBarra('Novo funcionário'), findsOneWidget);

      await tocar(tester, menu(AbaPerfil.advertencias));
      expect(naBarra('Gestão de advertências'), findsOneWidget);
    });

    testWidgets('Holerites ganha consulta da equipe e publicação de PDF', (tester) async {
      await abrir(tester, usuario('RH', nome: 'Bruno Lima', codigo: 'FUNC-0003'));

      await tocar(tester, menu(AbaPerfil.holerites));

      expect(find.text('Meus holerites'), findsOneWidget);
      expect(find.text('Holerites da equipe'), findsOneWidget);
      expect(find.text('Publicar holerite'), findsOneWidget);
    });
  });

  group('Raiz do app', () {
    testWidgets('no desktop a barra superior mostra a área e o menu continua à esquerda', (tester) async {
      await abrir(tester, usuario('Colaborador'), tamanho: const Size(1440, 900));

      expect(find.text('Área do colaborador'), findsOneWidget);
      expect(find.byTooltip('Holerites'), findsOneWidget);
      expect(find.text('Olá, Ana!'), findsOneWidget);
    });

    testWidgets('perfil desconhecido continua na tela provisória', (tester) async {
      await abrir(tester, usuario('Gestor'));

      expect(find.textContaining('Login validado'), findsOneWidget);
      expect(find.byKey(const Key('aba_inicio')), findsNothing);
    });
  });
}
```

## Arquivo substituído por inteiro

### hr_auto_login/lib/app.dart

```dart
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme/app_theme.dart';
import 'domain/entities/usuario_autenticado.dart';
import 'presentation/pages/admin/admin_page.dart';
import 'presentation/pages/auth_page.dart';
import 'presentation/pages/home_page.dart';
import 'presentation/pages/perfil/area_perfil_page.dart';
import 'presentation/providers.dart';

/// Raiz do app: tema e escolha da tela pela sessão
/// (sem sessão -> autenticação; Admin -> área do administrador; RH e Colaborador -> área do perfil;
/// qualquer outro valor de perfil -> tela provisória).
class HrAutoApp extends StatelessWidget {
  const HrAutoApp({super.key, this.usarGoogleFonts = true});

  /// false nos testes de widget (sem internet para baixar a fonte Inter).
  final bool usarGoogleFonts;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'HR-Auto',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.claro(usarGoogleFonts: usarGoogleFonts),
      home: const _RaizSessao(),
    );
  }
}

class _RaizSessao extends ConsumerWidget {
  const _RaizSessao();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessao = ref.watch(sessaoProvider);
    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 400),
      child: switch (sessao) {
        null => const AuthPage(key: ValueKey('auth')),
        UsuarioAutenticado(perfil: 'Admin') => AdminPage(key: const ValueKey('admin'), usuario: sessao),
        // A chave inclui o código: sair e entrar com outra conta recomeça no Início.
        UsuarioAutenticado(perfil: 'RH' || 'Colaborador') =>
          AreaPerfilPage(key: ValueKey('perfil_${sessao.codigo}'), usuario: sessao),
        _ => HomePage(key: const ValueKey('home'), usuario: sessao),
      },
    );
  }
}
```
