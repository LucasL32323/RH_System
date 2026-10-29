import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';
import '../core/files/arquivos.dart';
import '../core/network/api_client.dart';
import '../data/files/arquivos_plataforma.dart';
import '../data/repositories/admin_repository_api.dart';
import '../data/repositories/admin_repository_prototipo.dart';
import '../data/repositories/auth_repository_api.dart';
import '../data/repositories/auth_repository_prototipo.dart';
import '../data/repositories/holerite_repository_api.dart';
import '../data/repositories/holerite_repository_prototipo.dart';
import '../data/repositories/ponto_repository_api.dart';
import '../domain/entities/usuario_autenticado.dart';
import '../domain/repositories/admin_repository.dart';
import '../domain/repositories/auth_repository.dart';
import '../domain/repositories/holerite_repository.dart';
import '../domain/repositories/ponto_repository.dart';

/// ApiClient único do app. Sobrescreva nos testes.
final apiClientProvider = Provider<ApiClient>((ref) {
  final cliente = ApiClient();
  ref.onDispose(cliente.dispose);
  return cliente;
});

/// Repositório de autenticação: protótipo em memória ou API real (--dart-define=PROTOTIPO=false).
final authRepositoryProvider = Provider<AuthRepository>((ref) {
  if (AppConfig.modoPrototipo) return AuthRepositoryPrototipo();
  return AuthRepositoryApi(ref.watch(apiClientProvider));
});

/// Sessão atual. null = ninguém logado (o app mostra a tela de autenticação).
/// O token fica só em memória neste protótipo; o armazenamento seguro ainda está pendente.
final sessaoProvider = StateProvider<UsuarioAutenticado?>((ref) => null);

/// Dados das telas do Admin (dashboard, funcionários, advertências, compliance).
/// Com PROTOTIPO=false usa a API (Fase 5): funcionários, estrutura, advertências e ajustes de ponto são
/// reais; o que ainda não tem endpoint (indicadores do dashboard e compliance) continua com dados de exemplo.
final Provider<AdminRepository> adminRepositoryProvider = Provider<AdminRepository>((ref) {
  if (AppConfig.modoPrototipo) return AdminRepositoryPrototipo();
  return AdminRepositoryApi(
    ref.watch(apiClientProvider),
    token: () => ref.read(sessaoProvider)?.token,
    ponto: ref.watch(pontoRepositoryProvider),
    // Sem latência artificial: ela só serve para ver o carregamento no protótipo.
    prototipo: AdminRepositoryPrototipo(latencia: Duration.zero),
  );
});

/// Ponto de quem está logado. Com PROTOTIPO=false conversa com a API (/api/v1/ponto) usando o token da sessão;
/// no protótipo, usa o mesmo repositório em memória das telas do Admin.
final pontoRepositoryProvider = Provider<PontoRepository>((ref) {
  if (AppConfig.modoPrototipo) return ref.watch(adminRepositoryProvider);
  return PontoRepositoryApi(
    ref.watch(apiClientProvider),
    token: () => ref.read(sessaoProvider)?.token,
  );
});

/// Holerites (RF29, RF30). Com PROTOTIPO=false usa a API (/api/v1/holerites); no protótipo, dados em memória
/// do usuário logado (por isso o provider acompanha a sessão).
final holeriteRepositoryProvider = Provider<HoleriteRepository>((ref) {
  if (AppConfig.modoPrototipo) {
    final sessao = ref.watch(sessaoProvider);
    return HoleriteRepositoryPrototipo(
      codigoUsuario: sessao?.codigo ?? 'FUNC-0001',
      nomeUsuario: sessao?.nome ?? 'Administrador do Sistema',
      perfilUsuario: sessao?.perfil ?? 'Admin',
      latencia: const Duration(milliseconds: 300),
    );
  }
  return HoleriteRepositoryApi(
    ref.watch(apiClientProvider),
    token: () => ref.read(sessaoProvider)?.token,
  );
});

/// Seletor de arquivos do sistema (publicar holerite). Sobrescreva nos testes.
final seletorArquivoProvider = Provider<SeletorArquivo>((ref) => const SeletorArquivoPlataforma());

/// Entrega de arquivos baixados (PDF do holerite). Sobrescreva nos testes.
final salvadorArquivoProvider = Provider<SalvadorArquivo>((ref) => const SalvadorArquivoPlataforma());
