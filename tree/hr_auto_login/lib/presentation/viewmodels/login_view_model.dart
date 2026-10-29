import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/errors/falha.dart';
import '../providers.dart';
import 'estado_envio.dart';

/// ViewModel do login (MVVM): chama o repositório e grava a sessão em caso de sucesso.
/// A View só observa o [EstadoEnvio] e chama [entrar].
class LoginViewModel extends Notifier<EstadoEnvio> {
  @override
  EstadoEnvio build() => const EstadoEnvio();

  /// Devolve true se entrou. A troca para a tela inicial acontece sozinha (o app observa a sessão).
  Future<bool> entrar({required String login, required String senha}) async {
    if (state.carregando) return false; // ignora duplo clique

    state = const EstadoEnvio.carregando();
    try {
      final usuario = await ref.read(authRepositoryProvider).entrar(login: login, senha: senha);
      state = const EstadoEnvio();
      ref.read(sessaoProvider.notifier).state = usuario;
      return true;
    } on Falha catch (e) {
      state = EstadoEnvio(erro: e.mensagem);
      return false;
    } catch (_) {
      state = const EstadoEnvio(erro: 'Erro inesperado. Tente novamente.');
      return false;
    }
  }

  void limparErro() {
    if (state.erro != null) state = const EstadoEnvio();
  }
}

final loginViewModelProvider =
    NotifierProvider<LoginViewModel, EstadoEnvio>(LoginViewModel.new);
