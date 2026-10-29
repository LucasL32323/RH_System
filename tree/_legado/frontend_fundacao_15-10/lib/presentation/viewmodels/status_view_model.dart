import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../data/models/status_sistema.dart';
import '../../data/repositories/sistema_repository.dart';

/// Provider do ApiClient (um por app). Sobrescreva nos testes.
final apiClientProvider = Provider<ApiClient>((ref) {
  final cliente = ApiClient();
  ref.onDispose(cliente.dispose);
  return cliente;
});

/// Provider do repositório. Nos testes, troque por um fake.
final sistemaRepositoryProvider = Provider<SistemaRepository>(
  (ref) => SistemaRepositoryApi(ref.watch(apiClientProvider)),
);

/// Estado da tela de fundação.
enum FaseConexao { carregando, conectada, erro }

class StatusState {
  const StatusState({required this.fase, this.status, this.erro});

  const StatusState.carregando() : this(fase: FaseConexao.carregando);

  final FaseConexao fase;
  final StatusSistema? status;
  final String? erro;
}

/// ViewModel (MVVM): conversa com o repositório e expõe o estado para a View.
class StatusViewModel extends Notifier<StatusState> {
  @override
  StatusState build() {
    // Dispara a primeira consulta logo após a criação do ViewModel.
    Future.microtask(verificar);
    return const StatusState.carregando();
  }

  Future<void> verificar() async {
    state = const StatusState.carregando();
    try {
      final status = await ref.read(sistemaRepositoryProvider).obterStatus();
      state = StatusState(fase: FaseConexao.conectada, status: status);
    } on ApiException catch (e) {
      state = StatusState(fase: FaseConexao.erro, erro: e.mensagem);
    } catch (_) {
      state = const StatusState(
        fase: FaseConexao.erro,
        erro: 'Erro inesperado ao consultar a API.',
      );
    }
  }
}

final statusViewModelProvider =
    NotifierProvider<StatusViewModel, StatusState>(StatusViewModel.new);
