using JADirect.Domain.Enums;

namespace JADirect.Domain.Models;

/// <summary>Dados do motorista que não registrou o Daily Log no prazo.</summary>
public class PendingDriverAlert
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname {  get; set; } = string.Empty;
    public UserRoles Role { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool HasActiveSession { get; set; }
}