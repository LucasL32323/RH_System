import 'dart:convert';
import 'dart:typed_data';

import '../../core/errors/falha.dart';
import '../../domain/entities/holerite.dart';
import '../../domain/regras/regras_holerite.dart';
import '../../domain/repositories/holerite_repository.dart';

/// Holerites em memória para validar as telas sem subir a API (PROTOTIPO=true).
/// Simula as regras do servidor: só RH/Admin consulta ou publica para outra pessoa, ninguém publica o próprio,
/// competência repetida dá conflito sem "substituir". Os PDFs de exemplo são gerados na hora.
class HoleriteRepositoryPrototipo implements HoleriteRepository {
  HoleriteRepositoryPrototipo({
    this.codigoUsuario = 'FUNC-0001',
    this.nomeUsuario = 'Administrador do Sistema',
    this.perfilUsuario = 'Admin',
    this.latencia = Duration.zero,
    DateTime Function()? relogio,
  }) : _relogio = relogio ?? DateTime.now {
    // Três meses anteriores já publicados para o usuário do protótipo.
    final hoje = _relogio();
    for (var i = 1; i <= 3; i++) {
      final competencia = DateTime(hoje.year, hoje.month - i);
      var publicadoEm = DateTime(competencia.year, competencia.month + 1, 5);
      if (publicadoEm.isAfter(hoje)) publicadoEm = hoje; // nunca publicado no futuro
      _adicionar(codigoUsuario, nomeUsuario, competencia.month, competencia.year, publicadoEm);
    }
  }

  /// Quem está logado (o protótipo não tem sessão própria; o provider informa).
  final String codigoUsuario;
  final String nomeUsuario;
  final String perfilUsuario;

  /// Atraso artificial para ver o carregamento (zero nos testes).
  final Duration latencia;

  final DateTime Function() _relogio;

  final List<_Registro> _registros = [];
  int _proximoId = 1;

  /// Funcionários que o protótipo conhece (para consultar/publicar por código).
  static const Map<String, String> _conhecidos = {
    'FUNC-0002': 'Renata Lopes',
    'FUNC-0003': 'Bruno Lima',
    'FUNC-0004': 'Ana Souza',
  };

  bool get _rhOuAdmin => perfilUsuario == 'RH' || perfilUsuario == 'Admin';

  String? _nomeDe(String codigo) => codigo == codigoUsuario ? nomeUsuario : _conhecidos[codigo];

  /// Guarda o holerite com o [pdf] informado (ou um PDF de exemplo) e devolve a entidade criada.
  Holerite _adicionar(String codigo, String nome, int mes, int ano, DateTime publicadoEm, {Uint8List? pdf}) {
    final holerite = Holerite(
      id: _proximoId++,
      codigoFuncionario: codigo,
      nomeFuncionario: nome,
      mes: mes,
      ano: ano,
      dataPublicacao: DateTime(publicadoEm.year, publicadoEm.month, publicadoEm.day),
      nomeArquivo: 'holerite-$ano-${mes.toString().padLeft(2, '0')}.pdf',
    );
    _registros.add(_Registro(holerite, pdf ?? gerarPdfDeExemplo('Holerite ${holerite.competencia} - $codigo')));
    return holerite;
  }

  @override
  Future<List<Holerite>> listar({String? codigo, int? ano}) async {
    await Future<void>.delayed(latencia);

    var alvo = codigoUsuario;
    if (codigo != null && codigo.trim().isNotEmpty) {
      final normalizado = RegrasHolerite.normalizarCodigo(codigo);
      if (normalizado == null) throw const Falha('Código inválido. Use o formato FUNC-0001.');
      alvo = normalizado;
    }
    if (alvo != codigoUsuario) {
      if (!_rhOuAdmin) throw const Falha('Você só pode ver os seus próprios holerites.');
      if (_nomeDe(alvo) == null) throw const Falha('Funcionário não encontrado.');
    }

    final lista = [
      for (final r in _registros)
        if (r.holerite.codigoFuncionario == alvo && (ano == null || r.holerite.ano == ano)) r.holerite,
    ]..sort((a, b) => b.ano != a.ano ? b.ano.compareTo(a.ano) : (b.mes != a.mes ? b.mes.compareTo(a.mes) : b.id.compareTo(a.id)));
    return lista;
  }

  @override
  Future<ArquivoHolerite> baixar(Holerite holerite) async {
    await Future<void>.delayed(latencia);
    final registro = _registros.where((r) => r.holerite.id == holerite.id).firstOrNull;
    final permitido = registro != null && (registro.holerite.codigoFuncionario == codigoUsuario || _rhOuAdmin);
    if (!permitido) throw const Falha('Holerite não encontrado.');
    return ArquivoHolerite(nome: registro.holerite.nomeArquivo, bytes: registro.pdf);
  }

  @override
  Future<HoleritePublicado> publicar({
    required String codigo,
    required int mes,
    required int ano,
    required String nomeArquivo,
    required Uint8List conteudo,
    bool substituir = false,
  }) async {
    await Future<void>.delayed(latencia);

    if (!_rhOuAdmin) throw const Falha('Apenas o RH pode publicar holerites.');
    final codigoOk = RegrasHolerite.validarPublicacao(
      codigo: codigo,
      mes: mes,
      ano: ano,
      nomeArquivo: nomeArquivo,
      conteudo: conteudo,
    );
    if (codigoOk == codigoUsuario) throw const Falha('Você não pode publicar o seu próprio holerite.');
    final nome = _nomeDe(codigoOk);
    if (nome == null) throw const Falha('Funcionário não encontrado.');

    final hoje = _relogio();
    if (ano * 12 + mes > hoje.year * 12 + hoje.month) {
      throw const Falha('Não é possível publicar o holerite de um mês que ainda não começou.');
    }

    final indice = _registros.indexWhere(
      (r) => r.holerite.codigoFuncionario == codigoOk && r.holerite.ano == ano && r.holerite.mes == mes,
    );
    if (indice >= 0 && !substituir) {
      throw Falha('Já existe holerite de ${mes.toString().padLeft(2, '0')}/$ano para $codigoOk. Peça para substituir.');
    }

    if (indice >= 0) {
      final antigo = _registros[indice].holerite;
      final novo = Holerite(
        id: antigo.id,
        codigoFuncionario: antigo.codigoFuncionario,
        nomeFuncionario: antigo.nomeFuncionario,
        mes: mes,
        ano: ano,
        dataPublicacao: DateTime(hoje.year, hoje.month, hoje.day),
        nomeArquivo: antigo.nomeArquivo,
      );
      _registros[indice] = _Registro(novo, Uint8List.fromList(conteudo));
      return HoleritePublicado(holerite: novo, substituido: true);
    }

    final criado = _adicionar(codigoOk, nome, mes, ano, hoje, pdf: Uint8List.fromList(conteudo));
    return HoleritePublicado(holerite: criado, substituido: false);
  }

  /// PDF de uma página com [titulo], montado à mão (tabela xref com os deslocamentos reais).
  /// Serve para o botão "Baixar PDF" abrir um arquivo de verdade no protótipo.
  static Uint8List gerarPdfDeExemplo(String titulo) {
    // Só caracteres ASCII seguros dentro do texto do PDF.
    final seguro = titulo.replaceAll(RegExp(r'[^A-Za-z0-9 .:/-]'), '');
    final conteudo = 'BT /F1 18 Tf 72 720 Td ($seguro) Tj ET';
    final objetos = <String>[
      '<< /Type /Catalog /Pages 2 0 R >>',
      '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
      '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
      '<< /Length ${conteudo.length} >>\nstream\n$conteudo\nendstream',
      '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    ];

    final saida = StringBuffer('%PDF-1.4\n');
    final deslocamentos = <int>[];
    for (var i = 0; i < objetos.length; i++) {
      deslocamentos.add(saida.length);
      saida.write('${i + 1} 0 obj\n${objetos[i]}\nendobj\n');
    }
    final inicioXref = saida.length;
    saida.write('xref\n0 ${objetos.length + 1}\n0000000000 65535 f \n');
    for (final d in deslocamentos) {
      saida.write('${d.toString().padLeft(10, '0')} 00000 n \n');
    }
    saida.write('trailer\n<< /Size ${objetos.length + 1} /Root 1 0 R >>\nstartxref\n$inicioXref\n%%EOF\n');

    // Tudo ASCII: os deslocamentos em caracteres são iguais aos em bytes.
    return Uint8List.fromList(ascii.encode(saida.toString()));
  }
}

class _Registro {
  const _Registro(this.holerite, this.pdf);

  final Holerite holerite;
  final Uint8List pdf;
}
