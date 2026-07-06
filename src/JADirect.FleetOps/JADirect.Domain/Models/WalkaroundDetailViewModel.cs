namespace JADirect.Domain.Models;


/// <summary>
/// ViewModel com os dados completos de um walkaround check individual,
/// incluindo informações de veículo necessárias para geração de documentos.
/// Retornado pelo InspectionRepository.GetWalkaroundById e consumido
/// pelo WalkaroundPdfService para montagem do WalkaroundPdfData.
/// Separado do WalkaroundHistoryViewModel para preservar o contrato
/// de cada caso de uso sem acoplamento entre eles.
/// </summary>
public class WalkaroundDetailViewModel
{
    public int WalkaroundId { get; set; }
    public DateTime CheckDate { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public int Odometer { get; set; }
    public bool HasDefect { get; set; }
    public List<ChecklistItemResult> Items { get; set; } = new();
    
}