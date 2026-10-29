using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Cadastro;

/// <summary>Endereço como chega da API (tudo opcional para a falta virar mensagem clara).</summary>
public sealed record EntradaEndereco(
    string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cidade, string? Uf);

/// <summary>Dados do funcionário como chegam da API, ainda sem validar.</summary>
public sealed record EntradaFuncionario(
    string? Nome, string? Cpf, string? Rg, string? Pis, string? Ctps,
    DateOnly? DataNascimento, DateOnly? DataAdmissao,
    string? Email, string? Telefone,
    int? IdCargo, int? IdDepartamento, int? IdGestor,
    EntradaEndereco? Endereco);

/// <summary>Dados validados e normalizados (CPF só dígitos, e-mail em minúsculas, UF em maiúsculas...).</summary>
public sealed record DadosCadastroFuncionario(
    string Nome, string Cpf, string Rg, string? Pis, string? Ctps,
    DateOnly DataNascimento, DateOnly DataAdmissao,
    string Email, string? Telefone,
    int IdCargo, int IdDepartamento, int? IdGestor,
    string Cep, string Logradouro, string Numero, string? Complemento, string Bairro, string Cidade, string Uf);

/// <summary>
/// Regras do cadastro de funcionários (RF09, RF10, RF12, RF16). Lançam <see cref="DominioException"/> com a mensagem
/// pronta para o usuário; a Application converte em resposta 400. Quem pode cadastrar (RH/Admin) é checado na Application.
/// </summary>
public static class RegrasFuncionario
{
    public const int IdadeMinimaAnos = 14;       // CLT, art. 403: menor aprendiz a partir dos 14 anos
    public const int DiasMaximosAdmissaoFutura = 90;
    public const int TamanhoMinimoNome = 3;
    public const int TamanhoMaximoNome = 120;

    private static readonly HashSet<string> Ufs = new(StringComparer.Ordinal)
    {
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA",
        "PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    };

    /// <summary>Valida e normaliza os dados. Falha na primeira regra violada.</summary>
    public static DadosCadastroFuncionario Normalizar(EntradaFuncionario entrada, DateOnly hoje)
    {
        var nome = Texto(entrada.Nome, "O nome", TamanhoMinimoNome, TamanhoMaximoNome);
        if (nome.Any(char.IsControl)) throw new DominioException("O nome contém caracteres inválidos.");

        if (!Cpf.TentarNormalizar(entrada.Cpf, out var cpf)) throw new DominioException("CPF inválido.");
        var rg = Texto(entrada.Rg, "O RG", 5, 20);

        string? pis = null;
        if (!string.IsNullOrWhiteSpace(entrada.Pis))
        {
            pis = SomenteDigitos(entrada.Pis);
            if (pis.Length != 11) throw new DominioException("O PIS deve ter 11 dígitos.");
        }

        string? ctps = null;
        if (!string.IsNullOrWhiteSpace(entrada.Ctps))
            ctps = Texto(entrada.Ctps, "A CTPS", 1, 20);

        if (entrada.DataNascimento is not { } nascimento) throw new DominioException("Informe a data de nascimento.");
        if (entrada.DataAdmissao is not { } admissao) throw new DominioException("Informe a data de admissão.");
        if (nascimento.Year < 1900 || nascimento > hoje.AddYears(-IdadeMinimaAnos))
            throw new DominioException($"A data de nascimento é inválida (idade mínima de {IdadeMinimaAnos} anos).");
        if (admissao < nascimento.AddYears(IdadeMinimaAnos))
            throw new DominioException("A admissão não pode ser antes de o funcionário completar 14 anos.");
        if (admissao > hoje.AddDays(DiasMaximosAdmissaoFutura))
            throw new DominioException($"A admissão não pode ser mais de {DiasMaximosAdmissaoFutura} dias no futuro.");

        if (!EmailUsuario.TentarNormalizar(entrada.Email, out var email)) throw new DominioException("E-mail inválido.");

        string? telefone = null;
        if (!string.IsNullOrWhiteSpace(entrada.Telefone))
        {
            telefone = SomenteDigitos(entrada.Telefone);
            if (telefone.Length is not (10 or 11)) throw new DominioException("O telefone deve ter DDD e 8 ou 9 dígitos.");
        }

        if (entrada.IdCargo is not > 0) throw new DominioException("Informe o cargo.");
        if (entrada.IdDepartamento is not > 0) throw new DominioException("Informe o departamento.");
        if (entrada.IdGestor is <= 0) throw new DominioException("O gestor informado é inválido.");

        var e = entrada.Endereco ?? throw new DominioException("Informe o endereço.");
        var cep = SomenteDigitos(e.Cep);
        if (cep.Length != 8) throw new DominioException("O CEP deve ter 8 dígitos.");
        var uf = (e.Uf ?? string.Empty).Trim().ToUpperInvariant();
        if (!Ufs.Contains(uf)) throw new DominioException("UF inválida.");

        string? complemento = null;
        if (!string.IsNullOrWhiteSpace(e.Complemento)) complemento = Texto(e.Complemento, "O complemento", 1, 60);

        return new DadosCadastroFuncionario(
            nome, cpf, rg, pis, ctps, nascimento, admissao, email, telefone,
            entrada.IdCargo.Value, entrada.IdDepartamento.Value, entrada.IdGestor,
            cep,
            Texto(e.Logradouro, "O logradouro", 2, 100),
            Texto(e.Numero, "O número do endereço", 1, 10),
            complemento,
            Texto(e.Bairro, "O bairro", 2, 80),
            Texto(e.Cidade, "A cidade", 2, 80),
            uf);
    }

    /// <summary>Cria o funcionário (ativo). O Id e o código FUNC-0001 vêm do banco ao gravar.</summary>
    public static Funcionario Criar(DadosCadastroFuncionario d) => new()
    {
        Nome = d.Nome,
        DataNascimento = d.DataNascimento,
        DataAdmissao = d.DataAdmissao,
        Ativo = true,
        Email = d.Email,
        Telefone = d.Telefone,
        IdCargo = d.IdCargo,
        IdDepartamento = d.IdDepartamento,
        IdGestor = d.IdGestor
    };

    /// <summary>Documentos e endereço (1:1). Chamar depois que o funcionário tiver Id.</summary>
    public static FuncionarioDetalhes CriarDetalhes(int idFuncionario, DadosCadastroFuncionario d)
    {
        var detalhes = new FuncionarioDetalhes { IdFuncionario = idFuncionario };
        AplicarDetalhes(detalhes, d);
        return detalhes;
    }

    /// <summary>Copia os dados pessoais para o funcionário existente (não mexe em situação, desligamento nem vínculos de navegação).</summary>
    public static void Aplicar(Funcionario funcionario, DadosCadastroFuncionario d)
    {
        funcionario.Nome = d.Nome;
        funcionario.DataNascimento = d.DataNascimento;
        funcionario.DataAdmissao = d.DataAdmissao;
        funcionario.Email = d.Email;
        funcionario.Telefone = d.Telefone;
        funcionario.IdCargo = d.IdCargo;
        funcionario.IdDepartamento = d.IdDepartamento;
        funcionario.IdGestor = d.IdGestor;
    }

    public static void AplicarDetalhes(FuncionarioDetalhes detalhes, DadosCadastroFuncionario d)
    {
        detalhes.Cpf = d.Cpf;
        detalhes.Rg = d.Rg;
        detalhes.Pis = d.Pis;
        detalhes.Ctps = d.Ctps;
        detalhes.Endereco = d.Logradouro;
        detalhes.Numero = d.Numero;
        detalhes.Complemento = d.Complemento;
        detalhes.Bairro = d.Bairro;
        detalhes.Cidade = d.Cidade;
        detalhes.Estado = d.Uf;
        detalhes.Cep = d.Cep;
    }

    /// <summary>
    /// RF16: o gestor precisa estar ativo, não pode ser o próprio funcionário e não pode formar ciclo
    /// (A é gestor de B e B é gestor de A, direta ou indiretamente).
    /// </summary>
    /// <param name="idFuncionario">Funcionário que terá o gestor; 0 se ainda não foi gravado (não há ciclo possível).</param>
    /// <param name="gestor">Gestor escolhido.</param>
    /// <param name="cadeiaDoGestor">Ids do gestor e de todos os gestores acima dele.</param>
    public static void ValidarGestor(int idFuncionario, Funcionario gestor, IEnumerable<int> cadeiaDoGestor)
    {
        if (!gestor.Ativo) throw new DominioException("O gestor escolhido está desligado.");
        if (idFuncionario > 0 && gestor.Id == idFuncionario)
            throw new DominioException("O funcionário não pode ser gestor de si mesmo.");
        if (idFuncionario > 0 && cadeiaDoGestor.Contains(idFuncionario))
            throw new DominioException("O gestor escolhido já é subordinado deste funcionário (hierarquia circular).");
    }

    /// <summary>RF12: o desligamento grava a data e inativa o funcionário. O cadastro nunca é apagado (RNF07).</summary>
    public static void Desligar(Funcionario funcionario, DateOnly data, DateOnly hoje)
    {
        if (!funcionario.Ativo) throw new DominioException("O funcionário já está desligado.");
        if (data < funcionario.DataAdmissao) throw new DominioException("O desligamento não pode ser antes da admissão.");
        if (data > hoje) throw new DominioException("A data de desligamento não pode ser futura.");

        funcionario.Ativo = false;
        funcionario.DataDesligamento = data;
    }

    public static void Reativar(Funcionario funcionario)
    {
        if (funcionario.Ativo) throw new DominioException("O funcionário já está ativo.");
        funcionario.Ativo = true;
        funcionario.DataDesligamento = null;
    }

    /// <summary>Nomes dos campos que mudaram (sem os valores), para o log de auditoria não guardar dado pessoal.</summary>
    public static IReadOnlyList<string> CamposAlterados(Funcionario f, FuncionarioDetalhes? atual, DadosCadastroFuncionario d)
    {
        var campos = new List<string>();
        if (f.Nome != d.Nome) campos.Add("nome");
        if (f.DataNascimento != d.DataNascimento) campos.Add("nascimento");
        if (f.DataAdmissao != d.DataAdmissao) campos.Add("admissão");
        if (f.Email != d.Email) campos.Add("e-mail");
        if (f.Telefone != d.Telefone) campos.Add("telefone");
        if (f.IdCargo != d.IdCargo) campos.Add("cargo");
        if (f.IdDepartamento != d.IdDepartamento) campos.Add("departamento");
        if (f.IdGestor != d.IdGestor) campos.Add("gestor");
        if (atual is null)
        {
            campos.Add("documentos e endereço");
        }
        else
        {
            if (atual.Cpf != d.Cpf || atual.Rg != d.Rg || atual.Pis != d.Pis || atual.Ctps != d.Ctps) campos.Add("documentos");
            if (atual.Cep != d.Cep || atual.Endereco != d.Logradouro || atual.Numero != d.Numero
                || atual.Complemento != d.Complemento || atual.Bairro != d.Bairro
                || atual.Cidade != d.Cidade || atual.Estado != d.Uf) campos.Add("endereço");
        }
        return campos;
    }

    // ---- auxiliares ----

    private static string Texto(string? valor, string campo, int minimo, int maximo)
    {
        var texto = string.Join(' ', (valor ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (texto.Length < minimo)
            throw new DominioException(minimo <= 1 ? $"{campo} é obrigatório." : $"{campo} é obrigatório (mínimo de {minimo} caracteres).");
        if (texto.Length > maximo) throw new DominioException($"{campo} pode ter no máximo {maximo} caracteres.");
        return texto;
    }

    private static string SomenteDigitos(string? valor) =>
        new((valor ?? string.Empty).Where(c => c is >= '0' and <= '9').ToArray());
}
