namespace JADirect.Domain.Entities;

/// <summary>
/// Representa um item de inspeção do walkaround check.
/// Cada item pertence a um tenant e a um tipo de veículo específico.
/// </summary>
public class ChecklistItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int VehicleTypeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Label {  get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    
}