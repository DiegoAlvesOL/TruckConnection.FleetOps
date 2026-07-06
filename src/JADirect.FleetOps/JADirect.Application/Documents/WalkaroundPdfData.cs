using JADirect.Domain.Models;

namespace JADirect.Application.Documents;


/// <summary>
/// DTO com todos os dados necessários para geração do PDF do walkaround check.
/// Preenchido pelo WalkaroundPdfService e consumido pelo WalkaroundPdfDocument.
/// O document não acessa banco nem S3 — recebe este objeto já completo.
/// </summary>
public class WalkaroundPdfData
{
    public int WalkaroundId { get; set; }
    public DateTime CheckDate { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public int Odometer { get; set; }
    public bool HasDefect  { get; set; }
    public List<ChecklistItemResult> Items { get; set; } = new();
    public List<WalkaroundPdfPhotoData> Photos { get; set; } = new();
    
    /// <summary>
    /// Número de referência único do documento.
    /// Formato: WLK-{ano}-{id:D6}. Exemplo: WLK-2026-000181.
    /// Gerado a partir do WalkaroundId e do CheckDate para garantir unicidade
    /// e evitar que autoridades considerem o documento reutilizado.
    /// </summary>
    public string ReferenceNumber => string.Format("WLK-{0}-{1:D6}", CheckDate.Year, WalkaroundId);
}