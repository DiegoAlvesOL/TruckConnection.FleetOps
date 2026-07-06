namespace JADirect.Domain.Entities;


/// <summary>
/// Representa uma fotografia registrada durante um walkaround check.
/// Cada foto está vinculada a exatamente um item do checklist de um walkaround específico.
/// O arquivo físico é armazenado no Railway Bucket. Esta entidade guarda apenas
/// os metadados e a chave de acesso ao arquivo.
/// </summary>
public class WalkaroundPhoto
{
    public int Id { get; set; }
    public int WalkaroundId { get; set; }
    public int ChecklistItemId  { get; set; }
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    
    /// <summary>
    /// Caminho do arquivo no Railway Bucket.
    /// Formato: photos/{ano}/{mes}/{dia}/{walkaroundId}_{checklistItemId}_{uuidCurto}.jpg
    /// Exemplo: photos/2026/04/27/142_37_a3f9bc.jpg
    /// Este valor nunca é uma URL pública. O acesso é sempre intermediado pelo backend.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;
    public int FileSizeKb { get; set; }
    public DateTime TakenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}