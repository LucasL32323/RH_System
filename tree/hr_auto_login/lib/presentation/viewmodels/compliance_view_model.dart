import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/compliance.dart';
import '../providers.dart';

/// Central de Compliance: leituras e testes obrigatórios, perfil DISC e benefícios.
class ComplianceViewModel extends AsyncNotifier<CentralCompliance> {
  @override
  Future<CentralCompliance> build() => ref.read(adminRepositoryProvider).carregarCompliance();

  /// Publica um aviso de leitura obrigatória (RF15). Lança [Falha] se não gravar.
  Future<LeituraObrigatoria> publicarAviso({
    required String titulo,
    required String tipo,
    required DateTime expiraEm,
  }) async {
    final repositorio = ref.read(adminRepositoryProvider);
    final aviso = await repositorio.publicarAviso(titulo: titulo, tipo: tipo, expiraEm: expiraEm);
    state = AsyncData(await repositorio.carregarCompliance());
    return aviso;
  }
}

final complianceViewModelProvider =
    AsyncNotifierProvider<ComplianceViewModel, CentralCompliance>(ComplianceViewModel.new);
