using JADirect.Domain.Entities;
using JADirect.Domain.Enums;

namespace JADirect.Domain.Models;

/// <summary>
/// Modelo consolidado para o Dashboard do motorista.
/// As propriedades de estado da jornada (ActiveAssignment, HasWalkaroundToday, JourneyStep)
/// foram adicionadas para suportar o stepper e o hero card contextual da Fase 2.
/// As propriedades legadas (AvailableVehicles, RecentActivities) foram preservadas integralmente.
/// </summary>
public class DriverDashboardModel
{
    public IEnumerable<VehicleStatusViewModel> AvailableVehicles { get; set; } = new List<VehicleStatusViewModel>();
    public List<RecentActivityItem> RecentActivities { get; set; } = new List<RecentActivityItem>();
    public DriverAssignment? ActiveAssignment { get; set; }
    public bool HasWalkaroundToday { get; set; }
    public JourneyStep JourneyStep { get; set; } = JourneyStep.NeedsVehicle;
    public VehicleStatusViewModel? VehicleCompliance { get; set; }
}

/// <summary>
/// Estende os dados do veículo com informações de conformidade (semáforo).
/// </summary>
public class VehicleStatusViewModel
{
    public int Id { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int CurrentKm { get; set; }
    public DateTime? LastInspectionDate { get; set; }

    /// <summary>
    /// Cor do semáforo de conformidade. Valores possíveis: Red, Yellow, Green.
    /// </summary>
    public string StatusColor { get; set; } = "Red";

    /// <summary>
    /// Indica se o motorista está autorizado a iniciar o Daily Log neste veículo.
    /// </summary>
    public bool IsDailyLogAllowed { get; set; } = false;
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Item de atividade recente exibido na tabela de histórico do motorista.
/// </summary>
public class RecentActivityItem
{
    public DateTime LogDate { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public int Deliveries { get; set; }
    public int Collections { get; set; }
    public int Returns { get; set; }
    
}