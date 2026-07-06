namespace JADirect.Domain.Entities;

/// <summary>
/// Registra quando um motorista iniciou contato com o número da empresa.
/// Enquanto <see cref="ExpiresAt"/> for maior que o momento atual,
/// mensagens enviadas a esse motorista não consomem cota de template pago.
/// </summary>
public class WhatsappSession
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}