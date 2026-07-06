using JADirect.Domain.Enums;

namespace JADirect.Domain.Entities;

/// <summary>
/// Log de auditoria de cada tentativa de envio de mensagem WhatsApp.
/// Registrado independentemente de sucesso ou falha.
/// </summary>
public class WhatsappMessageLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? UserId { get; set; }
    public WhatsappMessageType MessageType { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public WhatsappMessageStatus Status { get; set; }
    public string? MetaMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
}