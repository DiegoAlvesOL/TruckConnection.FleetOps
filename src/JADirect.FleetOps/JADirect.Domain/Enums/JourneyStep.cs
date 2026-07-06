
namespace JADirect.Domain.Enums;

/// <summary>
/// Representa o passo atual da jornada diária do motorista.
/// O valor é calculado pelo DriverController com base no estado do assignment
/// e do walkaround check, e governa qual seção do dashboard é exibida.
/// </summary>
public enum JourneyStep
{
    NeedsVehicle = 0,
    NeedsWalkaround = 1,
    Ready = 2
}