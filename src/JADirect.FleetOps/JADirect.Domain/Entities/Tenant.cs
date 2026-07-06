namespace JADirect.Domain.Entities;


/// <summary>
/// Representa um tenant cadastrado na plataforma.
/// Os horários armazenados são interpretados no fuso horário definido em <see cref="Timezone"/>.
/// </summary>
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } =  string.Empty;
    public string WhatsappManagerPhone { get; set; } = string.Empty;
    public int DailyLogDeadlineHour { get; set; }
    public int AlertDriverHour { get; set; }
    public int AlertManagerHour { get; set; }
    public string Timezone { get; set; } = "Europe/Dublin";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}