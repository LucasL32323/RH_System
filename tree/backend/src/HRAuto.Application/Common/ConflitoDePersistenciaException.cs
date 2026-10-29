namespace HRAuto.Application.Common;

/// <summary>
/// O banco recusou a gravação por violar uma chave única (ex.: dois cliques simultâneos criando o PONTO do dia, RNF09).
/// A Infrastructure converte o erro do provedor nesta exceção; os casos de uso a transformam em falha de conflito (HTTP 409).
/// </summary>
public sealed class ConflitoDePersistenciaException : Exception
{
    public ConflitoDePersistenciaException(string mensagem, Exception? interna = null) : base(mensagem, interna) { }
}
