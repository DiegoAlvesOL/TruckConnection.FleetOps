namespace JADirect.Domain.Models;


/// <summary>
/// Representa o resultado de um item individual preenchido pelo motorista
/// durante o walkaround check.
/// </summary>
public class ChecklistItemResult
{
    public int ItemId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string? Note { get; set; }
}