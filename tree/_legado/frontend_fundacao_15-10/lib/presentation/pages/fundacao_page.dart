import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../viewmodels/status_view_model.dart';
import '../widgets/menu_lateral.dart';

/// Tela temporária da Fase 1: confirma que o app conversa com a API.
/// Será substituída pelo login e pelo dashboard nas fases seguintes.
class FundacaoPage extends ConsumerWidget {
  const FundacaoPage({super.key});

  static const _itens = [
    ItemMenu(icone: Icons.home_outlined, rotulo: 'Início'),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(statusViewModelProvider);

    return Scaffold(
      body: Row(
        children: [
          MenuLateral(
            itens: _itens,
            indiceSelecionado: 0,
            aoSelecionar: (_) {},
          ),
          // Conteúdo centralizado, com largura máxima para telas grandes.
          Expanded(
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 480),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: _Conteudo(estado: estado),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Conteudo extends ConsumerWidget {
  const _Conteudo({required this.estado});

  final StatusState estado;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tema = Theme.of(context).textTheme;

    return Card(
      elevation: 0,
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: AppColors.apoio),
      ),
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('HR-Auto', style: tema.headlineMedium),
            const SizedBox(height: 16),
            switch (estado.fase) {
              FaseConexao.carregando => const CircularProgressIndicator(),
              FaseConexao.conectada => Column(
                  children: [
                    const Icon(Icons.check_circle,
                        color: AppColors.semaforoVerde, size: 40),
                    const SizedBox(height: 8),
                    const Text('API conectada'),
                    Text(
                      '${estado.status!.nome} v${estado.status!.versao} '
                      '(${estado.status!.ambiente})',
                      style: tema.bodySmall,
                    ),
                  ],
                ),
              FaseConexao.erro => Column(
                  children: [
                    const Icon(Icons.error_outline,
                        color: AppColors.semaforoVermelho, size: 40),
                    const SizedBox(height: 8),
                    Text(estado.erro ?? 'Falha ao conectar.'),
                  ],
                ),
            },
            const SizedBox(height: 16),
            FilledButton(
              onPressed: estado.fase == FaseConexao.carregando
                  ? null
                  : () => ref.read(statusViewModelProvider.notifier).verificar(),
              child: const Text('Testar conexão'),
            ),
          ],
        ),
      ),
    );
  }
}
