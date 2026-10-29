import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/errors/falha.dart';
import '../../core/validation/validadores.dart';
import '../providers.dart';
import 'estado_envio.dart';

/// ViewModel do cadastro (MVVM). Normaliza os dados antes de enviar
/// (nome sem espaços duplicados, e-mail em minúsculas).
class CadastroViewModel extends Notifier<EstadoEnvio> {
  @override
  EstadoEnvio build() => const EstadoEnvio();

  /// Devolve true se a conta foi criada.
  Future<bool> cadastrar({
    required String nome,
    required String email,
    required String senha,
  }) async {
    if (state.carregando) return false;

    state = const EstadoEnvio.carregando();
    try {
      await ref.read(authRepositoryProvider).cadastrar(
            nome: Validadores.normalizarNome(nome),
            email: email.trim().toLowerCase(),
            senha: senha,
          );
      state = const EstadoEnvio();
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

final cadastroViewModelProvider =
    NotifierProvider<CadastroViewModel, EstadoEnvio>(CadastroViewModel.new);
