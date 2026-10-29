namespace HRAuto.Application.Abstractions;

/// <summary>
/// Hash de senha (RNF05): a senha do USUARIO nunca é guardada em texto puro.
/// A implementação (BCrypt) fica na Infrastructure; o login (Fase 4) usa este contrato.
/// </summary>
public interface IHashSenha
{
    /// <summary>Gera o hash BCrypt (com salt embutido) da senha informada.</summary>
    string Gerar(string senha);

    /// <summary>Confere se a senha digitada corresponde ao hash gravado em USUARIO.senha.</summary>
    bool Verificar(string senha, string hash);
}
