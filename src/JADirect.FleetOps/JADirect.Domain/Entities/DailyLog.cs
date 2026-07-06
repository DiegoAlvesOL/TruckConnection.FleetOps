namespace JADirect.Domain.Entities;

/// <summary>
/// Registra a performance diária de entregas, coletas e retornos. 
/// </summary>
public class DailyLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public int? AssignmentId { get; set; }
    public DateTime LogDate { get; set; }
    public int Deliveries { get; set; }
    public int Collections { get; set; }
    public int Returns { get; set; }
    
    /// <summary>
    /// Odômetro final opcional. Se não preenchido, o sistema mantém a última marcação conhecida.
    /// </summary>
    public int? CurrentOdometer { get; set; } 
    
    public string? Notes { get; set; }
}