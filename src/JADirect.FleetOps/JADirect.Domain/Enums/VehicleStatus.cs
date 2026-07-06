namespace JADirect.Domain.Enums;

/// <summary>
/// Define a situação operacional de um veículo da frota
/// </summary>
public enum VehicleStatus
{
    Active = 1,
    Maintenance = 2,
    Retired = 3,
    Blocked = 4
}