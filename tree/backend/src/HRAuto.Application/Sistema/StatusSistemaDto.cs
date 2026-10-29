namespace HRAuto.Application.Sistema;

/// <summary>Resposta do endpoint de status; o app Flutter usa para testar a conexão com a API.</summary>
/// <param name="Nome">Nome da aplicação.</param>
/// <param name="Versao">Versão do back-end.</param>
/// <param name="Ambiente">Development, Production etc.</param>
/// <param name="HoraServidor">Hora atual em Brasília (ISO 8601 com offset).</param>
public sealed record StatusSistemaDto(string Nome, string Versao, string Ambiente, DateTimeOffset HoraServidor);
