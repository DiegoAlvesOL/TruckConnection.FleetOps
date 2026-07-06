namespace JADirect.Domain.Models;

/// <summary>
/// Modelo de visualização para o histórico de auditoria.
/// Combina dados da inspeção com dados do usuário (motorista).
/// </summary>
public class WalkaroundHistoryViewModel
{
    public int WalkaroundId { get; set; }
    public DateTime CheckDate{ get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string RegistrationNo { get; set; } = string.Empty;
    public int Odometer { get; set; }
    public List<ChecklistItemResult> Items { get; set; } = new List<ChecklistItemResult>();
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public Dictionary<int, string> PhotosByItemId { get; set; } = new Dictionary<int, string>();
    
    public bool VehicleWasBlocked => Items.Any(item =>
        (item.State == "Defect" || item.State == "Attention") &&
        item.ActionTaken == "RequiresGarage");
    public bool IsPassed => !VehicleWasBlocked;
    
    public string? Location => Latitude.HasValue && Longitude.HasValue ?
        $"https://www.google.com/maps?q={Latitude},{Longitude}" :
        null;
}