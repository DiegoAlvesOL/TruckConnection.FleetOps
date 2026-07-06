namespace JADirect.Domain.Models;

/// <summary>
/// Modelo de visualização para o dashboard gerencial. 
/// Transporta os totais consolidados e calcula índices de produtividade.
/// </summary>
public class PerformanceReportViewModel
{
    // Intervalo de tempo do relatório
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? DriverSearch { get; set; }
    
    // Dados brutos vindos do Repositório
    public int TotalDeliveries { get; set; }
    public int TotalCollections { get; set; }
    public int TotalReturns { get; set; }
    public int TotalKmTraveled { get; set; }


    // Lista para o Ranking de Performance (Lado Esquerdo do Esboço) 
    public List<VehiclePerformanceSummary> DriverRanking { get; set; } = new();

    /// <summary>
    /// Lista para o Grid Detalhado de auditoria (Parte inferior do esboço).
    /// </summary>
    public List<DailyLogDetailItem> DetailedLogs { get; set; } = new();
    
    /// <summary>
    /// Lista para alertas de compliance e avisos
    /// </summary>
    public List<ComplianceExceptionViewModel> PendingDailyLogs { get; set; } = new();
    public List<ComplianceExceptionViewModel> PendingWalkarounds { get; set; } = new();


    /// <summary>
    /// Lista para armazenar veículos que estão com a inspeção próxima do vencimento.
    /// </summary>
    public List<ComplianceExceptionViewModel> ExpiringInspections { get; set; } = new();
    
    /// <summary>
    /// Propriedade calculada: Soma todas as ações e divide pela distância.
    /// Define a "eficiência" que será exibida no topo do dashboard.
    /// </summary>
    public decimal EfficiencyIndex => TotalKmTraveled > 0 
    ? (decimal)(TotalDeliveries + TotalCollections + TotalReturns)/TotalKmTraveled
    : 0;
}


/// <summary>
/// Representa uma linha de performance agregada por motorista.
/// </summary>
public class VehiclePerformanceSummary
{
    public string DriverName { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string RegistrationNo { get; set; } = string.Empty;
    public int Deliveries { get; set; }
    public int Collections { get; set; }
    public int Returns { get; set; }
    
    public string VehicleTypeDisplayName => int.TryParse(VehicleType, out int id) ?
        ((JADirect.Domain.Enums.VehicleType)id).ToString() : "N/A";
}


public class DailyLogDetailItem
{
    public DateTime LogDate { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string RegistrationNo { get; set; } = string.Empty;
    public int Deliveries { get; set; }
    public int Collections { get; set; }
    public int Returns { get; set; }
    public string Notes { get; set; } = string.Empty;
    
    public string VehicleTypeDisplayName => int.TryParse(VehicleType, out int id) ? 
        ((JADirect.Domain.Enums.VehicleType)id).ToString() : "N/A";
}