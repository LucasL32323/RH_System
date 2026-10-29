import 'dart:convert';

import 'identificador_login.dart';

/// Validações dos formulários. Espelham as regras do back-end (Fase 4), para o usuário
/// ver o erro antes de enviar; quem decide de verdade continua sendo a API.
///  - E-mail: mesmo padrão de EmailUsuario.cs (algo@dominio.ext, até 254 caracteres).
///  - Senha: mesma PoliticaSenha.cs (8+ caracteres, maiúscula, minúscula, número e símbolo; até 72 bytes).
class Validadores {
  Validadores._();

  static const int tamanhoMaximoEmail = 254;
  static const int tamanhoMinimoSenha = 8;
  static const int tamanhoMaximoSenhaBytes = 72; // limite do BCrypt
  static const int tamanhoMaximoNome = 150;

  static final RegExp _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  // Letras (com acento), espaço, apóstrofo, hífen e ponto: "Ana D'Ávila", "João S. Pereira-Lima".
  static final RegExp _nome = RegExp(r"^[\p{L}' .-]+$", unicode: true);

  /// Campo "E-mail ou usuário" do login: aceita o e-mail ou o código do funcionário (FUNC-0001).
  /// A busca é pelo código, nunca pelo nome; mesmas regras do AutenticarUsuario.cs.
  static String? loginUsuario(String? valor) {
    final texto = valor?.trim() ?? '';
    if (texto.isEmpty) return 'Informe o e-mail ou usuário.';
    if (IdentificadorLogin.tentarLer(texto) != null) return null;
    return texto.contains('@') ? 'E-mail inválido.' : 'Use o e-mail ou o código FUNC-0001.';
  }

  /// Senha do login: só exige preenchimento (a política vale na criação, não na entrada).
  static String? senhaLogin(String? valor) =>
      (valor == null || valor.isEmpty) ? 'Informe a senha.' : null;

  static String? email(String? valor) {
    final texto = valor?.trim() ?? '';
    if (texto.isEmpty) return 'Informe o e-mail.';
    if (texto.length > tamanhoMaximoEmail || !_email.hasMatch(texto)) return 'E-mail inválido.';
    return null;
  }

  static String? nomeCompleto(String? valor) {
    final texto = normalizarNome(valor ?? '');
    if (texto.isEmpty) return 'Informe o nome completo.';
    if (texto.length > tamanhoMaximoNome) return 'Nome muito longo (máximo de $tamanhoMaximoNome caracteres).';
    if (!_nome.hasMatch(texto)) return 'Use apenas letras no nome.';

    final partes = texto.split(' ').where((p) => p.replaceAll(RegExp(r"[.'-]"), '').length >= 2);
    if (partes.length < 2) return 'Informe nome e sobrenome.';
    return null;
  }

  /// Remove espaços repetidos e nas pontas ("  Ana   Souza " -> "Ana Souza").
  static String normalizarNome(String valor) => valor.trim().replaceAll(RegExp(r'\s+'), ' ');

  /// Nova senha (cadastro). Mesmas mensagens da PoliticaSenha do back-end.
  static String? senhaNova(String? valor) {
    if (valor == null || valor.isEmpty) return 'Informe a senha.';
    final requisitos = RequisitosSenha.avaliar(valor);
    if (!requisitos.dentroDoLimite) {
      return 'A senha é muito longa (máximo de $tamanhoMaximoSenhaBytes bytes).';
    }
    if (!requisitos.atende) {
      return 'A senha deve ter no mínimo 8 caracteres, com letra maiúscula, letra minúscula, número e símbolo.';
    }
    return null;
  }

  static String? confirmacaoSenha(String? valor, String senha) {
    if (valor == null || valor.isEmpty) return 'Confirme a senha.';
    if (valor != senha) return 'As senhas não conferem.';
    return null;
  }

  static bool pareceEmail(String texto) => _email.hasMatch(texto.trim());

  // ---- Cadastro de funcionário ----

  static String? obrigatorio(String? valor, String mensagem) =>
      (valor == null || valor.trim().isEmpty) ? mensagem : null;

  /// CPF com dígitos verificadores válidos (aceita com ou sem máscara).
  static String? cpf(String? valor) {
    final d = (valor ?? '').replaceAll(RegExp(r'\D'), '');
    if (d.isEmpty) return 'Informe o CPF.';
    if (!cpfValido(d)) return 'CPF inválido.';
    return null;
  }

  static bool cpfValido(String digitos) {
    if (digitos.length != 11 || RegExp(r'^(\d)\1{10}$').hasMatch(digitos)) return false;
    final n = digitos.split('').map(int.parse).toList();
    for (var etapa = 9; etapa <= 10; etapa++) {
      var soma = 0;
      for (var i = 0; i < etapa; i++) {
        soma += n[i] * (etapa + 1 - i);
      }
      final resto = soma % 11;
      if (n[etapa] != (resto < 2 ? 0 : 11 - resto)) return false;
    }
    return true;
  }

  /// Data no formato dd/mm/aaaa, dentro de [minima] e [maxima] quando informadas.
  static String? data(String? valor, {required String rotulo, DateTime? minima, DateTime? maxima}) {
    final texto = valor?.trim() ?? '';
    if (texto.isEmpty) return 'Informe a $rotulo.';
    final m = RegExp(r'^(\d{2})/(\d{2})/(\d{4})$').firstMatch(texto);
    if (m == null) return 'Use o formato dd/mm/aaaa.';
    final dia = int.parse(m.group(1)!);
    final mes = int.parse(m.group(2)!);
    final ano = int.parse(m.group(3)!);
    final data = DateTime(ano, mes, dia);
    if (data.day != dia || data.month != mes) return 'Data inexistente.';
    if (minima != null && data.isBefore(minima)) return 'Data muito antiga.';
    if (maxima != null && data.isAfter(maxima)) return 'Data no futuro não é permitida.';
    return null;
  }

  static String? telefone(String? valor) {
    final d = (valor ?? '').replaceAll(RegExp(r'\D'), '');
    if (d.isEmpty) return 'Informe o telefone.';
    if (d.length < 10 || d.length > 11) return 'Telefone com DDD: (13) 99999-9999.';
    return null;
  }

  static String? cep(String? valor) {
    final d = (valor ?? '').replaceAll(RegExp(r'\D'), '');
    if (d.isEmpty) return 'Informe o CEP.';
    if (d.length != 8) return 'CEP com 8 dígitos.';
    return null;
  }

  static String? uf(String? valor) {
    final texto = valor?.trim().toUpperCase() ?? '';
    if (texto.isEmpty) return 'UF';
    if (!_ufs.contains(texto)) return 'UF inválida.';
    return null;
  }

  static const Set<String> _ufs = {
    'AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA',
    'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO',
  };

  /// Horário hh:mm (ajuste de ponto).
  static String? horario(String? valor) {
    final m = RegExp(r'^([01]\d|2[0-3]):([0-5]\d)$').firstMatch(valor?.trim() ?? '');
    return m == null ? 'Use o formato hh:mm.' : null;
  }
}

/// Situação de cada regra da política de senha (alimenta o checklist da tela de cadastro).
class RequisitosSenha {
  const RequisitosSenha({
    required this.tamanho,
    required this.maiuscula,
    required this.minuscula,
    required this.numero,
    required this.simbolo,
    required this.dentroDoLimite,
  });

  final bool tamanho;
  final bool maiuscula;
  final bool minuscula;
  final bool numero;
  final bool simbolo;
  final bool dentroDoLimite;

  static final RegExp _maiuscula = RegExp(r'\p{Lu}', unicode: true);
  static final RegExp _minuscula = RegExp(r'\p{Ll}', unicode: true);
  static final RegExp _numero = RegExp(r'\p{Nd}', unicode: true);
  // Símbolo = nem letra, nem dígito, nem espaço (igual a !char.IsLetterOrDigit && !char.IsWhiteSpace no C#).
  static final RegExp _simbolo = RegExp(r'[^\p{L}\p{Nd}\s]', unicode: true);

  factory RequisitosSenha.avaliar(String senha) => RequisitosSenha(
        tamanho: senha.length >= Validadores.tamanhoMinimoSenha,
        maiuscula: _maiuscula.hasMatch(senha),
        minuscula: _minuscula.hasMatch(senha),
        numero: _numero.hasMatch(senha),
        simbolo: _simbolo.hasMatch(senha),
        dentroDoLimite: utf8.encode(senha).length <= Validadores.tamanhoMaximoSenhaBytes,
      );

  bool get atende => tamanho && maiuscula && minuscula && numero && simbolo && dentroDoLimite;

  /// Quantas das 5 regras visíveis foram cumpridas (0 a 5).
  int get pontuacao => [tamanho, maiuscula, minuscula, numero, simbolo].where((ok) => ok).length;
}
