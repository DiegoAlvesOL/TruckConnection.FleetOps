namespace JADirect.Domain.Enums;


/// <summary>
/// Define o status de um período de indisponibilidade de motorista.
/// Ciclo de vida: Active → Expired ou Canceled.
/// </summary>
public enum AvailabilityPeriodStatus
{
    /// <summary>
    /// Período vigente. Motorista está indisponível.
    /// Muda para Expired quando availability_to_date chega.
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Período finalizou naturalmente (availability_to_date foi atingida).
    /// Motorista foi reativado automaticamente.
    /// Preservado no histórico para auditoria e dashboard.
    Expired = 2,
    
    /// <summary>
    /// Período foi cancelado antes da data de retorno.
    /// Manager marcou como cancelado manualmente.
    /// Preservado no histórico para referência.
    /// </summary>
    Canceled = 3
    
}