import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/errors/falha.dart';
import '../providers.dart';
import 'estado_envio.dart';

/// ViewModel do diálogo "Esqueci minha senha".
/// autoDispose: cada vez que o diálogo abre, começa limpo.
class RecuperarSenhaViewModel extends AutoDisposeNotifier<EstadoEnvio> {
  @override
  EstadoEnvio build() => const EstadoEnvio();

  Future<void> solicitar(String email) async {
    if (state.carregando) return;

    state = const EstadoEnvio.carregando();
    try {
      final mensagem = await ref.read(authRepositoryProvider).solicitarRecuperacaoSenha(email);
      state = EstadoEnvio(sucesso: mensagem);
    } on Falha catch (e) {
      state = EstadoEnvio(erro: e.mensagem);
    } catch (_) {
      state = const EstadoEnvio(erro: 'Erro inesperado. Tente novamente.');
    }
  }
}

final recuperarSenhaViewModelProvider =
    NotifierProvider.autoDispose<RecuperarSenhaViewModel, EstadoEnvio>(RecuperarSenhaViewModel.new);
