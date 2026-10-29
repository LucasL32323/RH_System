import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/ponto.dart';
import '../providers.dart';

/// Registro de ponto de quem está logado: batida em um toque (RNF08) e pedido de ajuste (RF10).
/// Usa o [pontoRepositoryProvider]: API real com PROTOTIPO=false, memória no protótipo.
class PontoViewModel extends AsyncNotifier<EspelhoPonto> {
  bool _registrando = false;

  @override
  Future<EspelhoPonto> build() {
    // Trocar de usuário (sair e entrar com outra conta) recarrega o espelho do novo usuário.
    ref.watch(sessaoProvider.select((s) => s?.codigo));
    return ref.read(pontoRepositoryProvider).carregarPonto();
  }

  /// Registra a próxima batida da sequência. Devolve null se já havia um registro em andamento
  /// (duplo toque). Lança [Falha] se o servidor recusar.
  Future<Batida?> registrarBatida() async {
    if (_registrando) return null;
    _registrando = true;
    try {
      final repositorio = ref.read(pontoRepositoryProvider);
      final batida = await repositorio.registrarBatida();
      state = AsyncData(await repositorio.carregarPonto());
      return batida;
    } finally {
      _registrando = false;
    }
  }

  Future<AjustePonto> solicitarAjuste({
    required DateTime data,
    required TipoBatida tipoBatida,
    required String horarioSolicitado,
    required String justificativa,
  }) async {
    final repositorio = ref.read(pontoRepositoryProvider);
    final ajuste = await repositorio.solicitarAjuste(
      data: data,
      tipoBatida: tipoBatida,
      horarioSolicitado: horarioSolicitado,
      justificativa: justificativa,
    );
    state = AsyncData(await repositorio.carregarPonto());
    return ajuste;
  }
}

final pontoViewModelProvider = AsyncNotifierProvider<PontoViewModel, EspelhoPonto>(PontoViewModel.new);
