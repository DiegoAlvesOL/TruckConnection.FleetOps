using JADirect.Domain.Enums;

namespace JADirect.Domain.Entities;

/// <summary>
/// Representação de um veículo fisico da frota da JA Direct.
/// Contém informações de identificação, categoria e estado de conservação.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public int CurrentKm { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastWalkaroundAt { get; set; }
}
