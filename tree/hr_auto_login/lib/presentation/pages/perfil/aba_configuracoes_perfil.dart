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
