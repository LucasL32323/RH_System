using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Tests.Domain;

/// <summary>CPF, regras do cadastro de funcionário (RF09, RF10, RF12, RF16), hierarquia de perfis e notificações da disciplina.</summary>
public class CadastroTests
{
    private static readonly DateOnly Hoje = Apoio.D("2026-10-22");

    private static EntradaFuncionario Entrada(
        string? nome = "Maria da Silva", string? cpf = "529.982.247-25", string? rg = "12.345.678-9",
        string? pis = null, string? ctps = null, string? nascimento = "1995-05-05", string? admissao = "2026-10-01",
        string? email = "Maria@Empresa.com ", string? telefone = "(13) 99999-0000",
        int? idCargo = 1, int? idDepartamento = 1, int? idGestor = null, string? cep = "11010-000",
        string? logradouro = "Rua das Flores", string? numero = "100", string? bairro = "Centro", string? cidade = "Santos", string? uf = "sp") =>
        new(nome, cpf, rg, pis, ctps,
            nascimento is null ? null : Apoio.D(nascimento), admissao is null ? null : Apoio.D(admissao),
            email, telefone, idCargo, idDepartamento, idGestor,
            new EntradaEndereco(cep, logradouro, numero, null, bairro, cidade, uf));

    // ---------- CPF ----------

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData(" 529.982.247-25 ", "52998224725")]
    public void Cpf_Valido_NormalizaParaDigitos(string texto, string esperado)
    {
        Assert.True(Cpf.TentarNormalizar(texto, out var digitos));
        Assert.Equal(esperado, digitos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("529.982.247-24")]  // dígito verificador errado
    [InlineData("111.111.111-11")]  // todos iguais
    [InlineData("5299822472")]      // 10 dígitos
    [InlineData("529.982.247-255")] // 12 dígitos
    [InlineData("529a982b247c25")]  // letras não são aceitas
    public void Cpf_Invalido_ERecusado(string? texto) => Assert.False(Cpf.TentarNormalizar(texto, out _));

    [Fact]
    public void Cpf_FormatarEMascarar()
    {
        Assert.Equal("529.982.247-25", Cpf.Formatar("52998224725"));
        Assert.Equal("***.982.247-**", Cpf.Mascarar("52998224725"));
    }

    // ---------- Normalizar ----------

    [Fact]
    public void Normalizar_DadosValidos_PadronizaOsCampos()
    {
        var d = RegrasFuncionario.Normalizar(Entrada(idGestor: 3), Hoje);

        Assert.Equal("Maria da Silva", d.Nome);
        Assert.Equal("52998224725", d.Cpf);
        Assert.Equal("maria@empresa.com", d.Email);
        Assert.Equal("13999990000", d.Telefone);
        Assert.Equal("11010000", d.Cep);
        Assert.Equal("SP", d.Uf);
        Assert.Equal(3, d.IdGestor);
    }

    [Fact]
    public void Normalizar_EspacosRepetidosNoNome_SaoReduzidos()
    {
        var d = RegrasFuncionario.Normalizar(Entrada(nome: "  Maria    da   Silva "), Hoje);
        Assert.Equal("Maria da Silva", d.Nome);
    }

    [Theory]
    [InlineData("nome", null, "nome")]
    [InlineData("nome", "Al", "nome")]
    [InlineData("cpf", "123", "CPF")]
    [InlineData("rg", "12", "RG")]
    [InlineData("email", "sem-arroba", "E-mail")]
    [InlineData("telefone", "12345", "telefone")]
    [InlineData("pis", "123", "PIS")]
    [InlineData("cep", "123", "CEP")]
    [InlineData("uf", "XX", "UF")]
    [InlineData("logradouro", "", "logradouro")]
    [InlineData("numero", " ", "número")]
    [InlineData("bairro", "", "bairro")]
    [InlineData("cidade", "", "cidade")]
    public void Normalizar_CampoInvalido_LancaMensagemClara(string campo, string? valor, string trechoDaMensagem)
    {
        var entrada = campo switch
        {
            "nome" => Entrada(nome: valor),
            "cpf" => Entrada(cpf: valor),
            "rg" => Entrada(rg: valor),
            "email" => Entrada(email: valor),
            "telefone" => Entrada(telefone: valor),
            "pis" => Entrada(pis: valor),
            "cep" => Entrada(cep: valor),
            "uf" => Entrada(uf: valor),
            "logradouro" => Entrada(logradouro: valor),
            "numero" => Entrada(numero: valor),
            "bairro" => Entrada(bairro: valor),
            _ => Entrada(cidade: valor)
        };

        var e = Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(entrada, Hoje));
        Assert.Contains(trechoDaMensagem, e.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("2020-01-01")] // 6 anos
    [InlineData("1850-01-01")] // antes de 1900
    public void Normalizar_NascimentoImpossivel_LancaExcecao(string nascimento) =>
        Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(Entrada(nascimento: nascimento), Hoje));

    [Fact]
    public void Normalizar_AdmissaoAntesDos14Anos_LancaExcecao() =>
        Assert.Throws<DominioException>(() =>
            RegrasFuncionario.Normalizar(Entrada(nascimento: "2010-01-01", admissao: "2023-01-01"), Hoje));

    [Fact]
    public void Normalizar_AdmissaoMuitoNoFuturo_LancaExcecao() =>
        Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(Entrada(admissao: "2027-02-01"), Hoje));

    [Fact]
    public void Normalizar_AdmissaoProxima_EhAceita()
    {
        var d = RegrasFuncionario.Normalizar(Entrada(admissao: "2026-11-02"), Hoje);
        Assert.Equal(Apoio.D("2026-11-02"), d.DataAdmissao);
    }

    [Fact]
    public void Normalizar_SemEndereco_LancaExcecao()
    {
        var semEndereco = Entrada() with { Endereco = null };
        Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(semEndereco, Hoje));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Normalizar_SemCargoOuDepartamento_LancaExcecao(int? id)
    {
        Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(Entrada(idCargo: id), Hoje));
        Assert.Throws<DominioException>(() => RegrasFuncionario.Normalizar(Entrada(idDepartamento: id), Hoje));
    }

    // ---------- Criar / Aplicar ----------

    [Fact]
    public void Criar_GeraFuncionarioAtivo_EDetalhesDoMesmoId()
    {
        var d = RegrasFuncionario.Normalizar(Entrada(pis: "123.45678.90-1", ctps: "0001234"), Hoje);

        var f = RegrasFuncionario.Criar(d).ComId(7);
        var detalhes = RegrasFuncionario.CriarDetalhes(f.Id, d);

        Assert.True(f.Ativo);
        Assert.Equal("FUNC-0007", f.Codigo);
        Assert.Equal(7, detalhes.IdFuncionario);
        Assert.Equal("52998224725", detalhes.Cpf);
        Assert.Equal("12345678901", detalhes.Pis);
        Assert.Equal("Rua das Flores", detalhes.Endereco);
        Assert.Equal("SP", detalhes.Estado);
    }

    [Fact]
    public void CamposAlterados_ListaSomenteNomes_SemValores()
    {
        var d = RegrasFuncionario.Normalizar(Entrada(), Hoje);
        var f = RegrasFuncionario.Criar(d).ComId(1);
        var detalhes = RegrasFuncionario.CriarDetalhes(1, d);

        Assert.Empty(RegrasFuncionario.CamposAlterados(f, detalhes, d));

        var novo = RegrasFuncionario.Normalizar(Entrada(nome: "Maria Souza", cpf: "111.444.777-35", cidade: "Guarujá", idCargo: 2), Hoje);
        var campos = RegrasFuncionario.CamposAlterados(f, detalhes, novo);

        Assert.Contains("nome", campos);
        Assert.Contains("cargo", campos);
        Assert.Contains("documentos", campos);
        Assert.Contains("endereço", campos);
        Assert.DoesNotContain(campos, c => c.Contains("111") || c.Contains("Guarujá"));
    }

    // ---------- Gestor ----------

    [Fact]
    public void ValidarGestor_GestorAtivoESemCiclo_Passa()
    {
        var gestor = new Funcionario { Ativo = true }.ComId(3);
        RegrasFuncionario.ValidarGestor(5, gestor, new[] { 3, 1 });
    }

    [Fact]
    public void ValidarGestor_Desligado_LancaExcecao()
    {
        var gestor = new Funcionario { Ativo = false }.ComId(3);
        Assert.Throws<DominioException>(() => RegrasFuncionario.ValidarGestor(5, gestor, new[] { 3 }));
    }

    [Fact]
    public void ValidarGestor_ELeMesmo_LancaExcecao()
    {
        var gestor = new Funcionario { Ativo = true }.ComId(5);
        Assert.Throws<DominioException>(() => RegrasFuncionario.ValidarGestor(5, gestor, new[] { 5 }));
    }

    [Fact]
    public void ValidarGestor_HierarquiaCircular_LancaExcecao()
    {
        // 5 é gestor de 3 (3 -> 5); escolher 3 como gestor de 5 fecharia o ciclo.
        var gestor = new Funcionario { Ativo = true }.ComId(3);
        var e = Assert.Throws<DominioException>(() => RegrasFuncionario.ValidarGestor(5, gestor, new[] { 3, 5 }));
        Assert.Contains("circular", e.Message);
    }

    [Fact]
    public void ValidarGestor_FuncionarioNovo_NaoTemCiclo()
    {
        var gestor = new Funcionario { Ativo = true }.ComId(3);
        RegrasFuncionario.ValidarGestor(0, gestor, Array.Empty<int>());
    }

    // ---------- Desligar / reativar ----------

    [Fact]
    public void Desligar_GravaDataEInativa()
    {
        var f = new Funcionario { Ativo = true, DataAdmissao = Apoio.D("2024-01-10") };

        RegrasFuncionario.Desligar(f, Apoio.D("2026-10-20"), Hoje);

        Assert.False(f.Ativo);
        Assert.Equal(Apoio.D("2026-10-20"), f.DataDesligamento);
    }

    [Theory]
    [InlineData("2023-12-31")] // antes da admissão
    [InlineData("2026-10-23")] // futura
    public void Desligar_DataInvalida_LancaExcecao(string data)
    {
        var f = new Funcionario { Ativo = true, DataAdmissao = Apoio.D("2024-01-10") };
        Assert.Throws<DominioException>(() => RegrasFuncionario.Desligar(f, Apoio.D(data), Hoje));
        Assert.True(f.Ativo);
    }

    [Fact]
    public void Desligar_JaDesligado_LancaExcecao()
    {
        var f = new Funcionario { Ativo = false, DataAdmissao = Apoio.D("2024-01-10") };
        Assert.Throws<DominioException>(() => RegrasFuncionario.Desligar(f, Hoje, Hoje));
    }

    [Fact]
    public void Reativar_LimpaDataDeDesligamento()
    {
        var f = new Funcionario { Ativo = false, DataDesligamento = Apoio.D("2026-10-01") };

        RegrasFuncionario.Reativar(f);

        Assert.True(f.Ativo);
        Assert.Null(f.DataDesligamento);
        Assert.Throws<DominioException>(() => RegrasFuncionario.Reativar(f));
    }

    // ---------- Hierarquia de perfis ----------

    [Theory]
    [InlineData(PerfilAcesso.Admin, PerfilAcesso.Admin, true)]
    [InlineData(PerfilAcesso.Admin, PerfilAcesso.RH, true)]
    [InlineData(PerfilAcesso.Admin, PerfilAcesso.Colaborador, true)]
    [InlineData(PerfilAcesso.RH, PerfilAcesso.Colaborador, true)]
    [InlineData(PerfilAcesso.RH, PerfilAcesso.RH, false)]
    [InlineData(PerfilAcesso.RH, PerfilAcesso.Admin, false)]
    [InlineData(PerfilAcesso.Colaborador, PerfilAcesso.Colaborador, false)]
    public void RegrasAcesso_QuemPodeConcederEGerenciar(PerfilAcesso quem, PerfilAcesso alvo, bool esperado)
    {
        Assert.Equal(esperado, RegrasAcesso.PodeConceder(quem, alvo));
        Assert.Equal(esperado, RegrasAcesso.PodeGerenciar(quem, alvo));
    }

    // ---------- Senha temporária ----------

    [Fact]
    public void SenhaTemporaria_AtendeAPoliticaESempreMuda()
    {
        var senhas = Enumerable.Range(0, 50).Select(_ => GeradorSenhaTemporaria.Gerar()).ToList();

        Assert.All(senhas, s =>
        {
            Assert.Equal(24, s.Length);
            Assert.Null(PoliticaSenha.Validar(s));
        });
        Assert.Equal(senhas.Count, senhas.Distinct().Count());
    }

    // ---------- Cargos e departamentos ----------

    [Theory]
    [InlineData("  Analista   de   Sistemas ", "Analista de Sistemas")]
    [InlineData("TI", "TI")]
    public void NomeDeEstrutura_EhNormalizado(string entrada, string esperado) =>
        Assert.Equal(esperado, RegrasEstrutura.NormalizarNome(entrada, "cargo"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("A")]
    public void NomeDeEstrutura_Curto_LancaExcecao(string? entrada) =>
        Assert.Throws<DominioException>(() => RegrasEstrutura.NormalizarNome(entrada, "cargo"));

    [Fact]
    public void NomeDeEstrutura_Longo_LancaExcecao() =>
        Assert.Throws<DominioException>(() => RegrasEstrutura.NormalizarNome(new string('x', 81), "departamento"));

    [Fact]
    public void Descricao_VaziaViraNula_ELongaEhRecusada()
    {
        Assert.Null(RegrasEstrutura.NormalizarDescricao("   "));
        Assert.Equal("ok", RegrasEstrutura.NormalizarDescricao(" ok "));
        Assert.Throws<DominioException>(() => RegrasEstrutura.NormalizarDescricao(new string('x', 201)));
    }

    // ---------- Notificações da disciplina ----------

    [Fact]
    public void NotificacaoDeAdvertenciaRegistrada_VaiParaOFuncionario()
    {
        var advertencia = new Advertencia
        {
            IdFuncionario = 4, IdResponsavel = 2, Data = Apoio.D("2026-10-22"), Tipo = "Leve",
            Motivo = "Atrasos", Status = StatusAdvertencia.Ativa
        };

        var n = NotificacoesDisciplina.AdvertenciaRegistrada(advertencia, new DateTime(2026, 10, 22, 9, 0, 0));

        Assert.Equal(4, n.IdFuncionario);
        Assert.Equal(TiposNotificacao.Advertencia, n.Tipo);
        Assert.Contains("leve", n.Mensagem);
        Assert.Contains("22/10/2026", n.Mensagem);
        Assert.False(n.Lida);
    }

    [Fact]
    public void NotificacaoDeAdvertenciaCancelada_MencionaOTipo()
    {
        var advertencia = new Advertencia
        {
            IdFuncionario = 4, IdResponsavel = 2, Data = Apoio.D("2026-10-10"), Tipo = "Suspensao",
            Motivo = "Falta grave", Status = StatusAdvertencia.Cancelada
        };

        var n = NotificacoesDisciplina.AdvertenciaCancelada(advertencia, new DateTime(2026, 10, 22, 9, 0, 0));

        Assert.Equal("Advertência cancelada", n.Titulo);
        Assert.Contains("suspensão", n.Mensagem);
    }

    [Fact]
    public void NotificacaoDeAdvertencia_MotivoLongoEhResumido()
    {
        var advertencia = new Advertencia
        {
            IdFuncionario = 4, IdResponsavel = 2, Data = Apoio.D("2026-10-22"), Tipo = "Leve",
            Motivo = new string('m', 500), Status = StatusAdvertencia.Ativa
        };

        var n = NotificacoesDisciplina.AdvertenciaRegistrada(advertencia, DateTime.UtcNow);

        Assert.True(n.Mensagem.Length < 250);
        Assert.EndsWith("…", n.Mensagem);
    }
}
