using JADirect.Domain.Enums;


namespace JADirect.Domain.Entities;


/// <summary>
/// Representa um colaborador da JA Direct, seja ele gestor ou motorista.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRoles Role { get; set; }
    public UserStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}