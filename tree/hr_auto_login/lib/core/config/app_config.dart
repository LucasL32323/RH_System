/// Configuração do app, definida na linha de comando com --dart-define.
///
///   flutter run -d chrome                                   -> modo protótipo (sem API)
///   flutter run -d chrome --dart-define=PROTOTIPO=false     -> API .NET real em http://localhost:5080
///                                                              (perfil "http" do launchSettings)
///   ... --dart-define=API_URL=https://localhost:7080        -> perfil "https" (exige o certificado de dev confiável)
///
/// No emulador Android, "localhost" é o próprio emulador; use http://10.0.2.2:5080.
class AppConfig {
  AppConfig._();

  /// true: usa o repositório em memória (valida as telas sem subir o back-end).
  static const bool modoPrototipo = bool.fromEnvironment('PROTOTIPO', defaultValue: true);

  static const String apiBaseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://localhost:5080',
  );

  /// Tempo máximo de espera por resposta da API.
  static const Duration timeoutApi = Duration(seconds: 10);

  /// Conta de teste do modo protótipo (a mesma do seed de Development do back-end).
  static const String emailDemo = 'admin@hrauto.local';
  static const String senhaDemo = 'Admin@123';
}
