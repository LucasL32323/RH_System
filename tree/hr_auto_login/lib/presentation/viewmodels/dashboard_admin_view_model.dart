import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/errors/falha.dart';
import '../../domain/entities/dashboard_admin.dart';
import '../../domain/entities/ponto.dart';
import '../providers.dart';

/// ViewModel do dashboard do Admin (MVVM): carrega os indicadores e aplica as decisões
/// sobre os ajustes de ponto. A decisão aparece na hora (otimista) e volta se o repositório falhar.
class DashboardAdminViewModel extends AsyncNotifier<DashboardAdmin> {
  String? _ultimoErro;

  /// Mensagem da última decisão que falhou (já em português, vinda da API quando existe).
  /// Null se a última decisão deu certo ou ainda não houve nenhuma.
  String? get ultimoErro => _ultimoErro;

  @override
  Future<DashboardAdmin> build() => ref.read(adminRepositoryProvider).carregarDashboard();

  /// Aprova, recusa ou desfaz ([SituacaoAjuste.pendente]). [observacao] é o motivo da recusa.
  /// Devolve false se não foi gravado; nesse caso, [ultimoErro] explica quando a API disse o motivo.
  Future<bool> decidir(String id, SituacaoAjuste situacao, {String? observacao}) async {
    final anterior = _situacaoDe(id);
    if (anterior == null) return false;

    _ultimoErro = null;
    _alterar(id, situacao, observacao: observacao);
    try {
      await ref.read(adminRepositoryProvider).decidirAjuste(id: id, situacao: situacao, observacao: observacao);
      return true;
    } on Falha catch (e) {
      _ultimoErro = e.mensagem;
      _alterar(id, anterior);
      return false;
    } catch (_) {
      _alterar(id, anterior);
      return false;
    }
  }

  /// Recarrega do repositório (botão "Tentar novamente" e depois de mudanças em outras abas).
  Future<void> recarregar() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(adminRepositoryProvider).carregarDashboard());
  }

  SituacaoAjuste? _situacaoDe(String id) {
    for (final a in state.valueOrNull?.ajustes ?? const <AjustePonto>[]) {
      if (a.id == id) return a.situacao;
    }
    return null;
  }

  void _alterar(String id, SituacaoAjuste situacao, {String? observacao}) {
    final atual = state.valueOrNull;
    if (atual == null) return;
    state = AsyncData(atual.copyWith(
      ajustes: [
        for (final a in atual.ajustes) a.id == id ? a.copyWith(situacao: situacao, observacao: observacao) : a,
      ],
    ));
  }
}

final dashboardAdminViewModelProvider =
    AsyncNotifierProvider<DashboardAdminViewModel, DashboardAdmin>(DashboardAdminViewModel.new);
