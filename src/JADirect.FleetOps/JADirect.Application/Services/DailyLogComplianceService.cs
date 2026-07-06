using JADirect.Data.Repositories;
using JADirect.Domain.Models;

namespace JADirect.Application.Services;

/// <summary>
/// Serviço responsável por determinar quais motoristas não preencheram o Daily Log.
/// Consulta os repositórios e devolve a lista pronta para quem chamar.
/// Não envia mensagem. Não conhece WhatsApp.
/// </summary>
public class DailyLogComplianceService
{
    private readonly AlertRepository _alertRepository;
 
    public DailyLogComplianceService(AlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }
 
    /// <summary>
    /// Retorna os motoristas que não preencheram o Daily Log na data informada.
    /// Para cada motorista verifica se há sessão WhatsApp ativa nas últimas 24h.
    /// Inclui filtro de indisponibilidade do driver (férias/doença).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="date">Data de referência.</param>
    public List<PendingDriverAlert> GetPendingDrivers(int tenantId, DateOnly date)
    {
        var drivers = _alertRepository.GetDriversPendingDailyLog(tenantId, date);
 
        foreach (var driver in drivers)
        {
            driver.HasActiveSession = _alertRepository.HasActiveSession(driver.UserId);
        }
        return drivers;
    }
}
