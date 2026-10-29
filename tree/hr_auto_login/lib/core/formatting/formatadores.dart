/// Formatação em pt-BR sem depender do pacote intl (datas, moeda e percentuais do dashboard).
class Formatadores {
  Formatadores._();

  static const List<String> _meses = [
    'janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
    'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro',
  ];

  // DateTime.weekday: 1 = segunda ... 7 = domingo.
  static const List<String> _diasSemana = [
    'segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado', 'domingo',
  ];

  static String _dois(int n) => n.toString().padLeft(2, '0');

  static String _maiuscula(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);

  /// 21/10/2026
  static String data(DateTime d) => '${_dois(d.day)}/${_dois(d.month)}/${d.year}';

  /// 21/10
  static String diaMes(DateTime d) => '${_dois(d.day)}/${_dois(d.month)}';

  /// 09:14
  static String hora(DateTime d) => '${_dois(d.hour)}:${_dois(d.minute)}';

  /// outubro
  static String nomeMes(int mes) => _meses[(mes - 1) % 12];

  /// out
  static String mesAbreviado(DateTime d) => nomeMes(d.month).substring(0, 3);

  /// Outubro 2026
  static String mesAno(DateTime d) => '${_maiuscula(nomeMes(d.month))} ${d.year}';

  /// 10/2026 (competência da folha)
  static String competencia(DateTime d) => '${_dois(d.month)}/${d.year}';

  /// Quarta-feira, 21 de outubro de 2026
  static String dataPorExtenso(DateTime d) =>
      '${_maiuscula(_diasSemana[d.weekday - 1])}, ${d.day} de ${nomeMes(d.month)} de ${d.year}';

  /// "Hoje, 09:14", "Ontem, 18:40" ou "19/10, 11:27", relativo a [referencia].
  static String momentoRelativo(DateTime d, DateTime referencia) {
    final dia = DateTime(d.year, d.month, d.day);
    final hoje = DateTime(referencia.year, referencia.month, referencia.day);
    final dias = hoje.difference(dia).inDays;
    if (dias == 0) return 'Hoje, ${hora(d)}';
    if (dias == 1) return 'Ontem, ${hora(d)}';
    return '${diaMes(d)}, ${hora(d)}';
  }

  /// R$ 1.284.560,00
  static String moeda(double valor) {
    final centavos = (valor * 100).round();
    final sinal = centavos < 0 ? '-' : '';
    final absoluto = centavos.abs();
    return '${sinal}R\$ ${_milhar(absoluto ~/ 100)},${_dois(absoluto % 100)}';
  }

  /// R$ 1,28 mi
  static String moedaMilhoes(double valor) =>
      'R\$ ${(valor / 1000000).toStringAsFixed(2).replaceAll('.', ',')} mi';

  /// 2,4% ou +2,4% com [sinal].
  static String percentual(double valor, {int casas = 1, bool sinal = false}) {
    final texto = valor.abs().toStringAsFixed(casas).replaceAll('.', ',');
    final prefixo = valor < 0 ? '-' : (sinal ? '+' : '');
    return '$prefixo$texto%';
  }

  static String _milhar(int n) {
    final s = n.toString();
    final saida = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) saida.write('.');
      saida.write(s[i]);
    }
    return saida.toString();
  }

  static const Map<String, String> _acentos = {
    'á': 'a', 'à': 'a', 'â': 'a', 'ã': 'a', 'ä': 'a',
    'é': 'e', 'è': 'e', 'ê': 'e', 'ë': 'e',
    'í': 'i', 'ì': 'i', 'î': 'i', 'ï': 'i',
    'ó': 'o', 'ò': 'o', 'ô': 'o', 'õ': 'o', 'ö': 'o',
    'ú': 'u', 'ù': 'u', 'û': 'u', 'ü': 'u',
    'ç': 'c',
  };

  /// "12345678909" -> "123.456.789-09"
  static String cpf(String digitos) => digitos.length != 11
      ? digitos
      : '${digitos.substring(0, 3)}.${digitos.substring(3, 6)}.${digitos.substring(6, 9)}-${digitos.substring(9)}';

  /// "12345678909" -> "***.456.789-**" (RN09: CPF mascarado para quem não pode ver o dado completo).
  static String cpfMascarado(String digitos) =>
      digitos.length != 11 ? '***' : '***.${digitos.substring(3, 6)}.${digitos.substring(6, 9)}-**';

  /// "11015000" -> "11015-000"
  static String cep(String digitos) =>
      digitos.length != 8 ? digitos : '${digitos.substring(0, 5)}-${digitos.substring(5)}';

  /// Duração em horas e minutos: "8h12", "0h45"; com [sinal], "+1h30" ou "-0h20".
  static String duracao(Duration d, {bool sinal = false}) {
    final minutos = d.inMinutes.abs();
    final prefixo = d.isNegative ? '-' : (sinal ? '+' : '');
    return '$prefixo${minutos ~/ 60}h${_dois(minutos % 60)}';
  }

  /// Horas decimais: 312 -> "312 h", 38.5 -> "38,5 h".
  static String horas(double valor) {
    final inteiro = valor == valor.roundToDouble();
    final texto = inteiro ? _milhar(valor.round()) : valor.toStringAsFixed(1).replaceAll('.', ',');
    return '$texto h';
  }

  /// 3.4 -> "3,4 anos"; 1 -> "1 ano".
  static String anos(double valor) {
    if ((valor - 1).abs() < 0.05) return '1 ano';
    return '${valor.toStringAsFixed(1).replaceAll('.', ',')} anos';
  }

  /// "21/10/2026" -> DateTime; null se a data não existir.
  static DateTime? lerData(String texto) {
    final m = RegExp(r'^(\d{2})/(\d{2})/(\d{4})$').firstMatch(texto.trim());
    if (m == null) return null;
    final dia = int.parse(m.group(1)!);
    final mes = int.parse(m.group(2)!);
    final ano = int.parse(m.group(3)!);
    final data = DateTime(ano, mes, dia);
    if (data.day != dia || data.month != mes || data.year != ano) return null;
    return data;
  }

  /// Só os dígitos de um texto com máscara ("123.456.789-09" -> "12345678909").
  static String digitos(String texto) => texto.replaceAll(RegExp(r'\D'), '');

  /// Minúsculas e sem acentos, para busca ("João" encontra "joao").
  static String normalizarBusca(String texto) =>
      texto.toLowerCase().split('').map((c) => _acentos[c] ?? c).join().trim();
}
