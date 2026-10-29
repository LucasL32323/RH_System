import 'colaborador_resumo.dart';
import 'estrutura_empresa.dart';

/// Endereço de residência (dados complementares de FUNCIONARIOS_DETALHES).
class Endereco {
  const Endereco({
    required this.cep,
    required this.logradouro,
    required this.numero,
    this.complemento = '',
    required this.bairro,
    required this.cidade,
    required this.uf,
  });

  final String cep; // só dígitos
  final String logradouro;
  final String numero;
  final String complemento;
  final String bairro;
  final String cidade;
  final String uf;

  /// "Rua Azevedo Sodré, 120 - Santos/SP"
  String get resumo => '$logradouro, $numero - $cidade/$uf';
}

/// Funcionário (FUNCIONARIOS + FUNCIONARIOS_DETALHES). O código FUNC é gerado em sequência,
/// é único e não muda depois do cadastro (RN10).
class Funcionario {
  const Funcionario({
    required this.id,
    required this.nome,
    required this.cpf,
    required this.rg,
    required this.dataNascimento,
    required this.dataAdmissao,
    required this.email,
    required this.telefone,
    required this.cargo,
    required this.setor,
    required this.nivel,
    required this.endereco,
    this.gestor,
    this.ativo = true,
  });

  final int id;
  final String nome;
  final String cpf; // 11 dígitos, sem máscara
  final String rg;
  final DateTime dataNascimento;
  final DateTime dataAdmissao;
  final String email;
  final String telefone;
  final Cargo cargo;
  final String setor;
  final String? gestor;
  final NivelAcesso nivel;
  final Endereco endereco;
  final bool ativo;

  String get codigo => codigoDe(id);

  ColaboradorResumo get resumo => ColaboradorResumo(nome: nome, matricula: codigo, departamento: setor);

  /// 142 -> "FUNC-0142"
  static String codigoDe(int id) => 'FUNC-${id.toString().padLeft(4, '0')}';

  Funcionario copyWith({bool? ativo}) => Funcionario(
        id: id,
        nome: nome,
        cpf: cpf,
        rg: rg,
        dataNascimento: dataNascimento,
        dataAdmissao: dataAdmissao,
        email: email,
        telefone: telefone,
        cargo: cargo,
        setor: setor,
        gestor: gestor,
        nivel: nivel,
        endereco: endereco,
        ativo: ativo ?? this.ativo,
      );
}

/// Registro de quem viu os dados pessoais completos de um funcionário (RF22, Lei 13.709/2018).
class ConsultaDadosPessoais {
  const ConsultaDadosPessoais({required this.funcionario, required this.consultadoPor, required this.momento});

  final ColaboradorResumo funcionario;
  final String consultadoPor; // "FUNC-0001 · Administrador do Sistema"
  final DateTime momento;
}

/// Dados preenchidos na tela "Novo funcionário" (o id e o código vêm do repositório).
class NovoFuncionario {
  const NovoFuncionario({
    required this.nome,
    required this.cpf,
    required this.rg,
    required this.dataNascimento,
    required this.dataAdmissao,
    required this.email,
    required this.telefone,
    required this.cargo,
    required this.setor,
    required this.nivel,
    required this.endereco,
    required this.enviarConvite,
  });

  final String nome;
  final String cpf;
  final String rg;
  final DateTime dataNascimento;
  final DateTime dataAdmissao;
  final String email;
  final String telefone;
  final Cargo cargo;
  final Setor setor;
  final NivelAcesso nivel;
  final Endereco endereco;

  /// Cria o login (USUARIO) e envia o convite para o e-mail corporativo.
  final bool enviarConvite;
}
