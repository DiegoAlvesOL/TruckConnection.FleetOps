using JADirect.Domain.Enums;

namespace JADirect.Domain.Entities;


/// <summary>
/// Representa um período em que um motorista está indisponível
/// (férias, licença, etc). Vinculado a um tenant e a um driver específicos.
/// </summary>
public class AvailabilityPeriod
{
    public int Id { get; set; }
    public int TenantId {  get; set; }
    public int DriverId { get; set; }
    public UserStatus StatusDuringPeriod { get; set; }
    public DateTime AvailabilityFromDate { get; set; }
    public DateTime AvailabilityToDate { get; set; }
    public string? Reason  { get; set; }
    public bool AutoReactivate { get; set; }
    
    /// <summary>
    /// Status do período de indisponibilidade: Active, Expired, ou Canceled.
    /// Usado para distinguir períodos vigentes de histórico.
    /// </summary>
    public AvailabilityPeriodStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}