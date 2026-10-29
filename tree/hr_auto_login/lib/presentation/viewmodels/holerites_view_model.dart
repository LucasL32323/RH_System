import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/errors/falha.dart';
import '../../core/files/arquivos.dart';
import '../../domain/entities/holerite.dart';
import '../../domain/regras/regras_holerite.dart';
import '../../domain/repositories/holerite_repository.dart';
import '../providers.dart';

/// Baixa o PDF na API e o entrega ao usuário (download no navegador ou arquivo no aparelho).
/// Devolve onde o arquivo ficou. Lança [Falha] se a API recusar ou se não der para salvar.
Future<String> _baixarESalvar(HoleriteRepository repositorio, SalvadorArquivo salvador, Holerite holerite) async {
  final arquivo = await repositorio.baixar(holerite);
  return salvador.salvarPdf(nome: arquivo.nome, bytes: arquivo.bytes);
}

/// "Meus holerites" (RF30): os holerites de quem está logado, do mais recente para o mais antigo.
/// Vale para todos os perfis; o servidor só devolve os do próprio usuário.
class HoleritesViewModel extends AsyncNotifier<List<Holerite>> {
  @override
  Future<List<Holerite>> build() {
    // Trocar de usuário (sair e entrar com outra conta) recarrega a lista do novo usuário.
    ref.watch(sessaoProvider.select((s) => s?.codigo));
    return ref.read(holeriteRepositoryProvider).listar();
  }

  /// Botão "Tentar novamente" e atualização depois de uma publicação.
  Future<void> recarregar() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(holeriteRepositoryProvider).listar());
  }

  /// Baixa e salva o PDF. Lança [Falha] com a mensagem para o usuário se não der certo.
  Future<String> baixar(Holerite holerite) =>
      _baixarESalvar(ref.read(holeriteRepositoryProvider), ref.read(salvadorArquivoProvider), holerite);
}

final holeritesViewModelProvider = AsyncNotifierProvider<HoleritesViewModel, List<Holerite>>(HoleritesViewModel.new);

/// Resultado da consulta de um funcionário pelo código FUNC.
class ConsultaHolerites {
  const ConsultaHolerites({required this.codigo, required this.holerites});

  final String codigo;
  final List<Holerite> holerites;

  /// Nome vindo dos próprios holerites; vazio quando o funcionário ainda não tem nenhum.
  String get nome => holerites.isEmpty ? '' : holerites.first.nomeFuncionario;
}

/// Consulta de holerites da equipe (RH/Admin, RNF08): sempre pelo código FUNC, nunca pelo nome.
/// O servidor audita cada PDF baixado de outra pessoa. Estado nulo = nenhuma consulta feita ainda.
class ConsultaHoleritesViewModel extends AsyncNotifier<ConsultaHolerites?> {
  @override
  Future<ConsultaHolerites?> build() {
    ref.watch(sessaoProvider.select((s) => s?.codigo));
    return Future.value(null);
  }

  /// Consulta o funcionário. Aceita "func-0004" ou só "4". Código inválido, sem permissão ou inexistente
  /// ficam como erro no estado (a tela mostra a mensagem da [Falha]).
  Future<void> consultar(String codigoDigitado) async {
    final codigo = RegrasHolerite.normalizarCodigo(codigoDigitado);
    if (codigo == null) {
      state = AsyncError(const Falha('Código inválido. Use o formato FUNC-0001.'), StackTrace.current);
      return;
    }
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final lista = await ref.read(holeriteRepositoryProvider).listar(codigo: codigo);
      return ConsultaHolerites(codigo: codigo, holerites: lista);
    });
  }

  /// Refaz a consulta aberta (depois de publicar ou substituir um holerite desse funcionário).
  Future<void> atualizar() async {
    final atual = state.valueOrNull;
    if (atual != null) await consultar(atual.codigo);
  }

  void limpar() => state = const AsyncData(null);

  Future<String> baixar(Holerite holerite) =>
      _baixarESalvar(ref.read(holeriteRepositoryProvider), ref.read(salvadorArquivoProvider), holerite);
}

final consultaHoleritesViewModelProvider =
    AsyncNotifierProvider<ConsultaHoleritesViewModel, ConsultaHolerites?>(ConsultaHoleritesViewModel.new);

/// Publicação de holerite (RF29, só RH/Admin). O estado é "enviando": evita duplo clique no botão.
class PublicarHoleriteViewModel extends Notifier<bool> {
  @override
  bool build() => false;

  /// Publica o PDF. Lança [Falha] com a mensagem da API (ex.: competência repetida sem "substituir").
  Future<HoleritePublicado> publicar({
    required String codigo,
    required int mes,
    required int ano,
    required ArquivoSelecionado arquivo,
    bool substituir = false,
  }) async {
    if (state) throw const Falha('Já existe uma publicação em andamento. Aguarde um instante.');
    state = true;
    try {
      final resultado = await ref.read(holeriteRepositoryProvider).publicar(
            codigo: codigo,
            mes: mes,
            ano: ano,
            nomeArquivo: arquivo.nome,
            conteudo: arquivo.bytes,
            substituir: substituir,
          );

      // Se a consulta aberta é desse funcionário, a lista passa a mostrar o holerite novo.
      final consulta = ref.read(consultaHoleritesViewModelProvider).valueOrNull;
      if (consulta != null && consulta.codigo == resultado.holerite.codigoFuncionario) {
        await ref.read(consultaHoleritesViewModelProvider.notifier).atualizar();
      }
      return resultado;
    } finally {
      state = false;
    }
  }
}

final publicarHoleriteViewModelProvider = NotifierProvider<PublicarHoleriteViewModel, bool>(PublicarHoleriteViewModel.new);
