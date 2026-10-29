namespace HRAuto.Domain.Enums;

/// <summary>As quatro batidas do dia, na ordem obrigatória (RF17/RF18).</summary>
public enum TipoBatida
{
    Entrada = 1,
    SaidaIntervalo = 2,
    RetornoIntervalo = 3,
    Saida = 4
}

/// <summary>Natureza do dia para o cálculo de horas (RF20): feriados e férias não contam como dia de trabalho.</summary>
public enum ContextoDia
{
    DiaUtil = 1,
    DescansoSemanal = 2,
    Feriado = 3,
    Ferias = 4
}

/// <summary>Situação do dia depois de olhar as batidas.</summary>
public enum SituacaoDia
{
    /// <summary>Quatro batidas registradas.</summary>
    Completo = 1,
    /// <summary>Dia ainda aberto: faltam batidas, mas o dia não acabou.</summary>
    EmAndamento = 2,
    /// <summary>Dia encerrado com batidas faltando: precisa de manutenção de ponto (RF22).</summary>
    Inconsistente = 3,
    /// <summary>Dia útil encerrado sem nenhuma batida.</summary>
    Falta = 4,
    /// <summary>Feriado, férias ou descanso semanal sem batidas: nada a cobrar.</summary>
    SemExpediente = 5
}

/// <summary>Tipos de advertência (coluna ADVERTENCIA.tipo, gravada como texto com o nome do enum).</summary>
public enum TipoAdvertencia
{
    Leve = 1,
    Suspensao = 2,
    JustaCausa = 3
}

/// <summary>Cores do semáforo de advertências (RF47).</summary>
public enum NivelSemaforo
{
    Verde = 1,
    Amarelo = 2,
    Vermelho = 3
}

/// <summary>Resultado da régua de consequências (RF34): o sistema só sugere, quem decide é o RH.</summary>
public enum AcaoSugerida
{
    Nenhuma = 0,
    Suspensao = 1,
    JustaCausa = 2
}

/// <summary>Resultado de aplicar uma batida vinda da fila offline (RF19/RNF09).</summary>
public enum ResultadoSincronizacao
{
    Aplicada = 1,
    /// <summary>A mesma batida (mesmo tipo e horário) já estava gravada: nada foi duplicado.</summary>
    JaRegistrada = 2
}

/// <summary>Status de solicitações de férias e de manutenção de ponto (colunas de texto).</summary>
public static class StatusSolicitacao
{
    public const string Pendente = "Pendente";
    public const string Aprovada = "Aprovada";
    public const string Rejeitada = "Rejeitada";
}

/// <summary>Status de uma advertência. Só as "Ativas" contam no semáforo (RF47) e na régua (RF34).</summary>
public static class StatusAdvertencia
{
    public const string Ativa = "Ativa";
    public const string Cancelada = "Cancelada";
}

/// <summary>BANCO_HORAS.tipo.</summary>
public static class TiposBancoHoras
{
    public const string Credito = "Credito";
    public const string Debito = "Debito";
}

/// <summary>BANCO_HORAS.origem.</summary>
public static class OrigensBancoHoras
{
    /// <summary>Lançamento automático gerado pelo fechamento do ponto do dia.</summary>
    public const string Ponto = "Ponto";
    /// <summary>Ajuste manual feito pelo RH.</summary>
    public const string AjusteRH = "AjusteRH";
}

/// <summary>FERIADO.tipo (RF28).</summary>
public static class TiposFeriado
{
    public const string Nacional = "Nacional";
    public const string Estadual = "Estadual";
    public const string Municipal = "Municipal";
}

/// <summary>NOTIFICACAO.tipo (RF25, RF36).</summary>
public static class TiposNotificacao
{
    public const string HoraExtra = "HoraExtra";
    public const string Advertencia = "Advertencia";
    public const string Ferias = "Ferias";
    public const string ManutencaoPonto = "ManutencaoPonto";
    public const string Aviso = "Aviso";
}
