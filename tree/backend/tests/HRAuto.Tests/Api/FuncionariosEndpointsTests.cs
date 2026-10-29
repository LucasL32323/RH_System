using System.Net;
using System.Net.Http.Json;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>
/// RF06 a RF16, RF33, RF34, RF47: cadastro de funcionários, acessos, cargos, departamentos e advertências
/// pela API de verdade (SQLite, JWT, relógio fixo em 21/10/2026). Cada teste cria seus próprios dados com CPF e e-mail únicos.
/// </summary>
public class FuncionariosEndpointsTests : IClassFixture<ApiFabrica>
{
    private static int _semente = 100_000_000;

    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public FuncionariosEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    /// <summary>CPF válido e diferente a cada chamada (os 9 primeiros dígitos mudam, os 2 últimos são calculados).</summary>
    private static string CpfValido()
    {
        var d = new int[11];
        var n = Interlocked.Increment(ref _semente);
        for (var i = 0; i < 9; i++) d[i] = (n / (int)Math.Pow(10, 8 - i)) % 10;
        if (d.Take(9).Distinct().Count() == 1) d[8] = (d[8] + 1) % 10;

        int Digito(int quantos)
        {
            var soma = 0;
            for (var i = 0; i < quantos; i++) soma += d[i] * (quantos + 1 - i);
            var resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }

        d[9] = Digito(9);
        d[10] = Digito(10);
        return string.Concat(d);
    }

    private static object Cadastro(string cpf, string email, int idCargo, int idDepartamento, string? gestor = null) => new
    {
        nome = "Diego Alves", cpf, rg = "123456789", dataNascimento = "1999-03-04", dataAdmissao = "2026-10-19",
        email, telefone = "13999990000", idCargo, idDepartamento, codigoGestor = gestor,
        endereco = new { cep = "11010-000", logradouro = "Rua das Flores", numero = "100", bairro = "Centro", cidade = "Santos", uf = "SP" }
    };

    private async Task<(int Cargo, int Departamento)> CargoEDepartamentoAsync(string tokenRh)
    {
        var cargos = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/cargos", tokenRh))).LerJsonAsync();
        var departamentos = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/departamentos", tokenRh))).LerJsonAsync();
        return (cargos[0].GetProperty("id").GetInt32(), departamentos[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task RhCadastra_ConviteChegaPorEmail_FuncionarioDefineASenha_EEntra()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var (cargo, departamento) = await CargoEDepartamentoAsync(tokenRh);
        var email = $"{Guid.NewGuid():N}@hrauto.local";

        var cadastro = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", tokenRh,
            Cadastro(CpfValido(), email, cargo, departamento)));

        Assert.Equal(HttpStatusCode.Created, cadastro.StatusCode);
        var criado = await cadastro.LerJsonAsync();
        var codigo = criado.GetProperty("codigo").GetString()!;
        Assert.StartsWith("FUNC-", codigo);
        Assert.True(criado.GetProperty("conviteEnviado").GetBoolean());

        // O link do convite é o mesmo do "esqueci minha senha": definir a senha e entrar.
        var mensagem = _fabrica.Email.Enviados.Last(e => e.Para == email);
        var token = Uri.UnescapeDataString(mensagem.Corpo.Split("token=")[1].Split('\n')[0].Trim());
        var redefinir = await _http.PostAsJsonAsync("/api/v1/auth/redefinir-senha", new RedefinirSenhaDto(token, "SenhaNova@2026"), Json);
        Assert.Equal(HttpStatusCode.NoContent, redefinir.StatusCode);

        var tokenNovo = await _http.EntrarAsync(email, "SenhaNova@2026");
        var eu = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", tokenNovo))).LerJsonAsync();
        Assert.Equal(codigo, eu.GetProperty("codigo").GetString());
        Assert.Equal("Colaborador", eu.GetProperty("perfil").GetString());

        Assert.Contains("CadastrarFuncionario", await _fabrica.AcoesDeAuditoriaAsync(rh.IdUsuario));
    }

    [Fact]
    public async Task Cadastro_FichaMostraCpfFormatado_ERhAuditaAConsulta()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var (cargo, departamento) = await CargoEDepartamentoAsync(tokenRh);
        var cpf = CpfValido();
        var criado = await (await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", tokenRh,
            Cadastro(cpf, $"{Guid.NewGuid():N}@hrauto.local", cargo, departamento)))).LerJsonAsync();
        var codigo = criado.GetProperty("codigo").GetString()!;

        var ficha = await (await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/funcionarios/{codigo}", tokenRh))).LerJsonAsync();

        Assert.Equal($"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}", ficha.GetProperty("documentos").GetProperty("cpf").GetString());
        Assert.Equal("Santos", ficha.GetProperty("endereco").GetProperty("cidade").GetString());
        Assert.Contains("ConsultaDadosPessoais", await _fabrica.AcoesDeAuditoriaAsync(rh.IdUsuario));
    }

    [Fact]
    public async Task Cadastro_CpfRepetido_Recebe409_ColaboradorRecebe403_SemTokenRecebe401()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var colaborador = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var (cargo, departamento) = await CargoEDepartamentoAsync(tokenRh);
        var cpf = CpfValido();
        await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", tokenRh,
            Cadastro(cpf, $"{Guid.NewGuid():N}@hrauto.local", cargo, departamento)));

        var repetido = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", tokenRh,
            Cadastro(cpf, $"{Guid.NewGuid():N}@hrauto.local", cargo, departamento)));
        var comoColaborador = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", await _http.EntrarAsync(colaborador.Email),
            Cadastro(CpfValido(), $"{Guid.NewGuid():N}@hrauto.local", cargo, departamento)));
        var semToken = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/funcionarios"));

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, comoColaborador.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
    }

    [Fact]
    public async Task Desligar_BloqueiaOLogin_ReativarNaoDevolveOAcesso()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);

        var desligar = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/funcionarios/{ana.Codigo}/desligamento", tokenRh));
        var loginDesligada = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(ana.Email, ApiFabrica.SenhaPadrao), Json);
        var reativar = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/funcionarios/{ana.Codigo}/reativacao", tokenRh));
        var loginReativada = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(ana.Email, ApiFabrica.SenhaPadrao), Json);
        var acesso = await _http.SendAsync(Pedido(HttpMethod.Patch, $"/api/v1/funcionarios/{ana.Codigo}/acesso", tokenRh, new { ativo = true }));
        var loginComAcesso = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(ana.Email, ApiFabrica.SenhaPadrao), Json);

        Assert.Equal(HttpStatusCode.NoContent, desligar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginDesligada.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reativar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginReativada.StatusCode); // o acesso continua bloqueado
        Assert.Equal(HttpStatusCode.OK, acesso.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginComAcesso.StatusCode);
    }

    [Fact]
    public async Task Gestor_ListaSoAEquipe_EVeAFichaSemDocumentos()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        await _fabrica.CriarUsuarioAsync("Carla Dias");
        var tokenGestor = await _http.EntrarAsync(gestor.Email);

        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/funcionarios", tokenGestor))).LerJsonAsync();
        var ficha = await (await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/funcionarios/{ana.Codigo}", tokenGestor))).LerJsonAsync();

        Assert.Equal(1, lista.GetProperty("total").GetInt32());
        Assert.Equal(ana.Codigo, lista.GetProperty("itens")[0].GetProperty("codigo").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, ficha.GetProperty("documentos").ValueKind);
    }

    [Fact]
    public async Task Cargos_CriarEmUsoNaoExclui_LivreExclui()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var nome = $"Cargo {Guid.NewGuid():N}"[..20];

        var criado = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/cargos", tokenRh, new { nome }));
        var id = (await criado.LerJsonAsync()).GetProperty("id").GetInt32();
        var (_, departamento) = await CargoEDepartamentoAsync(tokenRh);
        await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/funcionarios", tokenRh,
            Cadastro(CpfValido(), $"{Guid.NewGuid():N}@hrauto.local", id, departamento)));

        var emUso = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/cargos/{id}", tokenRh));
        var outro = await (await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/cargos", tokenRh, new { nome = nome + "x" }))).LerJsonAsync();
        var livre = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/cargos/{outro.GetProperty("id").GetInt32()}", tokenRh));

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, emUso.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, livre.StatusCode);
    }

    [Fact]
    public async Task Advertencias_TresLeves_SemaforoVermelho_ReguaSugereSuspensao()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);

        HttpResponseMessage ultima = null!;
        for (var i = 1; i <= 3; i++)
            ultima = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/advertencias", tokenRh,
                new { codigoFuncionario = ana.Codigo, tipo = "Leve", motivo = $"Atraso {i}" }));

        Assert.Equal(HttpStatusCode.Created, ultima.StatusCode);
        var corpo = await ultima.LerJsonAsync();
        Assert.Equal("Vermelho", corpo.GetProperty("semaforo").GetProperty("nivel").GetString());
        Assert.Equal("Suspensao", corpo.GetProperty("regua").GetProperty("acao").GetString());

        var tokenAna = await _http.EntrarAsync(ana.Email);
        var situacao = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/advertencias/situacao", tokenAna))).LerJsonAsync();
        Assert.Equal("Vermelho", situacao.GetProperty("semaforo").GetProperty("nivel").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, situacao.GetProperty("regua").ValueKind);

        var id = corpo.GetProperty("advertencia").GetProperty("id").GetInt32();
        var cancelada = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/advertencias/{id}/cancelamento", tokenRh, new { motivo = "Registrada por engano" }));
        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        var depois = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/advertencias/situacao", tokenAna))).LerJsonAsync();
        Assert.Equal("Amarelo", depois.GetProperty("semaforo").GetProperty("nivel").GetString());
    }

    [Fact]
    public async Task Advertencias_ColaboradorNaoRegistra_NemListaTodas()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Lima");
        var tokenAna = await _http.EntrarAsync(ana.Email);

        var registrar = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/advertencias", tokenAna,
            new { codigoFuncionario = bruno.Codigo, tipo = "Leve", motivo = "Teste" }));
        var listar = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/advertencias", tokenAna));
        var situacaoDoOutro = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/advertencias/situacao/{bruno.Codigo}", tokenAna));

        Assert.Equal(HttpStatusCode.Forbidden, registrar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, situacaoDoOutro.StatusCode);
    }
}
