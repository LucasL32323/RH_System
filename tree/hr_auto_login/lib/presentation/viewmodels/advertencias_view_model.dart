import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/advertencia.dart';
import '../providers.dart';
import 'dashboard_admin_view_model.dart';

/// Gestão de advertências: registro, régua de consequências e aprovação de suspensões.
class AdvertenciasViewModel extends AsyncNotifier<List<Advertencia>> {
  @override
  Future<List<Advertencia>> build() => ref.read(adminRepositoryProvider).listarAdvertencias();

  /// Registra e devolve a suspensão gerada pela régua (RN07), se houver. Lança [Falha] se o código não existir.
  Future<Advertencia?> registrar({
    required String codigoFuncionario,
    required MotivoAdvertencia motivo,
    required String descricao,
    required String registradaPor,
  }) async {
    final repositorio = ref.read(adminRepositoryProvider);
    final suspensao = await repositorio.registrarAdvertencia(
      codigoFuncionario: codigoFuncionario,
      motivo: motivo,
      descricao: descricao,
      registradaPor: registradaPor,
    );
    state = AsyncData(await repositorio.listarAdvertencias());
    ref.invalidate(dashboardAdminViewModelProvider); // semáforo e alertas mudaram
    return suspensao;
  }

  Future<void> aprovarSuspensao(String id) async {
    final repositorio = ref.read(adminRepositoryProvider);
    await repositorio.aprovarSuspensao(id);
    state = AsyncData(await repositorio.listarAdvertencias());
    ref.invalidate(dashboardAdminViewModelProvider);
  }
}

final advertenciasViewModelProvider =
    AsyncNotifierProvider<AdvertenciasViewModel, List<Advertencia>>(AdvertenciasViewModel.new);
