/// Configuração do app. A URL da API muda por ambiente:
///   flutter run --dart-define=API_URL=https://localhost:7080
/// No emulador Android, "localhost" é o próprio emulador; use http://10.0.2.2:5080.
class AppConfig {
  AppConfig._();

  static const String apiBaseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://localhost:5080',
  );

  /// Tempo máximo de espera por resposta da API.
  static const Duration timeoutApi = Duration(seconds: 10);
}
