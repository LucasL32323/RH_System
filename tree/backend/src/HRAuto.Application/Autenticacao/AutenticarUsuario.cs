using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Autenticacao;

/// <summary>
/// Corpo de POST /auth/login. Campos opcionais para que a falta deles vire mensagem clara, não erro genérico.
/// "Login" aceita o e-mail OU o código do funcionário (FUNC-0001); "Email" continua aceito por compatibilidade
/// com o app antigo. Se os dois vierem preenchidos, vale "Login".
/// </summary>
public sealed record CredenciaisDto(string? Email, string? Senha, string? Login = null);

public sealed record UsuarioAutenticadoDto(string Codigo, string Nome, string Email, string Perfil);

public sealed record LoginRespostaDto(string Token, DateTimeOffset ExpiraEm, UsuarioAutenticadoDto Usuario);

/// <summary>
/// Login com e-mail ou código FUNC e senha (RF01, RNF04, RNF05). Devolve o token JWT com o perfil.
/// Cuidados de segurança:
/// - a mesma mensagem para identificador inexistente, mal formado, senha errada e conta inativa (não revela quem tem conta);
/// - conta inexistente também gasta um BCrypt, para o tempo de resposta não denunciar o identificador;
/// - a conta inativa só é reconhecida depois de a senha estar correta;
/// - login e falha de login ficam no log de auditoria (RF08, RNF07).
/// O código FUNC-0001 é derivado de FUNCIONARIO.id_funcionario (o modelo físico não tem coluna de código).
/// </summary>
public sealed class AutenticarUsuario
{
    public const string MensagemCredenciaisInvalidas = "E-mail ou senha inválidos.";
    public const string MensagemCamposObrigatorios = "Informe o e-mail (ou o código FUNC) e a senha.";

    // Hash descartável para gastar o mesmo tempo do BCrypt quando o identificador não existe. Criado uma vez por processo.
    private static string? _hashFalso;

    private readonly IUsuarioRepositorio _usuarios;
    private readonly IHashSenha _hash;
    private readonly IServicoToken _tokens;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;
    private readonly IUsuarioGestaoRepositorio _acessos;

    public AutenticarUsuario(
        IUsuarioRepositorio usuarios,
        IHashSenha hash,
        IServicoToken tokens,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio,
        IUsuarioGestaoRepositorio acessos)
    {
        _usuarios = usuarios;
        _hash = hash;
        _tokens = tokens;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
        _acessos = acessos;
    }

    public async Task<Resultado<LoginRespostaDto>> ExecutarAsync(CredenciaisDto? credenciais, CancellationToken cancelamento = default)
    {
        // "Login" (e-mail ou FUNC) tem prioridade; "Email" é o campo antigo.
        var digitado = credenciais is null
            ? null
            : string.IsNullOrWhiteSpace(credenciais.Login) ? credenciais.Email : credenciais.Login;

        if (credenciais is null || string.IsNullOrWhiteSpace(digitado) || string.IsNullOrEmpty(credenciais.Senha))
            return Resultado<LoginRespostaDto>.Falha(MensagemCamposObrigatorios);

        // Tem "@" = e-mail; senão tem de ser um código FUNC-0001. A busca é sempre pelo código, nunca pelo nome.
        var identificador = digitado.Trim();
        var porCodigo = !identificador.Contains('@');

        Usuario? usuario;
        if (porCodigo)
        {
            // Código fora do formato nunca existe no banco: mesma resposta de qualquer outra credencial inválida.
            if (!CodigoFuncionario.TentarConverter(identificador, out var codigo))
                return CredenciaisInvalidas();

            usuario = await _acessos.ObterPorFuncionarioAsync(codigo.Id, cancelamento);
        }
        else
        {
            if (!EmailUsuario.TentarNormalizar(identificador, out var email))
                return CredenciaisInvalidas();

            usuario = await _usuarios.ObterPorEmailAsync(email, cancelamento);
        }

        if (usuario is null)
        {
            _hash.Verificar(credenciais.Senha, ObterHashFalso());
            return CredenciaisInvalidas();
        }

        if (!_hash.Verificar(credenciais.Senha, usuario.Senha))
        {
            _auditoria.Registrar(usuario.Id, "LoginFalha", "USUARIO", usuario.Id, "Senha incorreta.");
            await _unidade.SalvarAsync(cancelamento);
            return CredenciaisInvalidas();
        }

        var funcionario = usuario.Funcionario;
        var perfilValido = Enum.IsDefined(typeof(PerfilAcesso), usuario.IdPerfil);
        if (!usuario.Ativo || funcionario is null || !funcionario.Ativo || !perfilValido)
        {
            _auditoria.Registrar(usuario.Id, "LoginNegado", "USUARIO", usuario.Id, "Usuário ou funcionário inativo.");
            await _unidade.SalvarAsync(cancelamento);
            return CredenciaisInvalidas();
        }

        var perfil = (PerfilAcesso)usuario.IdPerfil;
        usuario.UltimoLogin = _relogio.Agora().DateTime;
        _auditoria.Registrar(usuario.Id, "Login", "USUARIO", usuario.Id, porCodigo ? "Entrada pelo código FUNC." : null);
        await _unidade.SalvarAsync(cancelamento);

        var codigoFunc = CodigoFuncionario.Formatar(funcionario.Id);
        var token = _tokens.GerarAcesso(new DadosToken(usuario.Id, funcionario.Id, codigoFunc, funcionario.Nome, usuario.Email, perfil));

        return Resultado<LoginRespostaDto>.Ok(new LoginRespostaDto(
            token.Token,
            token.ExpiraEm,
            new UsuarioAutenticadoDto(codigoFunc, funcionario.Nome, usuario.Email, NomesPerfil.De(perfil))));
    }

    private static Resultado<LoginRespostaDto> CredenciaisInvalidas() =>
        Resultado<LoginRespostaDto>.Falha(TipoErro.NaoAutenticado, MensagemCredenciaisInvalidas);

    private string ObterHashFalso() => _hashFalso ??= _hash.Gerar(Guid.NewGuid().ToString("N"));
}
