using JADirect.Domain.Enums;

namespace JADirect.Domain.Entities;

/// <summary>
/// Representa uma inspeção de segurança veicular (Walkaround Check).
/// O status diferencia inspeções em rascunho das concluídas.
/// </summary>
public class WalkaroundCheck
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public int? AssignmentId { get; set; }
    public int Odometer { get; set; }
    public bool HasDefect { get; set; }
    public WalkaroundCheckStatus Status { get; set; } = WalkaroundCheckStatus.Completed;
    public string ChecklistJson { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}