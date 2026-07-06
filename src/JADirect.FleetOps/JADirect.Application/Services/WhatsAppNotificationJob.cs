using JADirect.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;


/// <summary>
/// Serviço de background que orquestra os alertas WhatsApp de conformidade.
/// Calcula o próximo horário de disparo entre todos os tenants ativos e dorme
/// até 1 minuto antes, acordando no máximo duas vezes por dia por tenant.
/// </summary>
public class WhatsAppNotificationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppNotificationJob> _logger;
 
    private readonly Dictionary<string, DateOnly> _lastDriverAlertDate = new();
    private readonly Dictionary<string, DateOnly> _lastManagerAlertDate = new();
 
    public WhatsAppNotificationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppNotificationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
 
    /// <summary>
    /// Loop principal. Calcula o próximo horário de alerta, dorme até 1 minuto antes
    /// e verifica a cada 60 segundos durante uma janela de 3 minutos ao redor do horário.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsAppNotificationJob started.");
 
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime nextAlertUtc = CalculateNextAlertUtc();
            TimeSpan waitTime = nextAlertUtc.AddMinutes(-1) - DateTime.UtcNow;
 
            if (waitTime > TimeSpan.Zero)
            {
                _logger.LogInformation(
                    "WhatsAppNotificationJob: next alert at {NextRun:yyyy-MM-dd HH:mm} UTC (in {Minutes:F0} min).",
                    nextAlertUtc, waitTime.TotalMinutes);
 
                await Task.Delay(waitTime, stoppingToken);
            }
 
            DateTime windowEnd = nextAlertUtc.AddMinutes(2);
 
            while (!stoppingToken.IsCancellationRequested && DateTime.UtcNow <= windowEnd)
            {
                await RunCheckAsync();
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
 
        _logger.LogInformation("WhatsAppNotificationJob stopped.");
    }
    
    /// <summary>
    /// Calcula em UTC o próximo horário de alerta entre todos os tenants ativos.
    /// Converte corretamente do fuso de cada tenant considerando horário de verão.
    /// </summary>
    private DateTime CalculateNextAlertUtc()
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<TenantRepository>();
        var activeTenants = tenantRepository.GetAllActive();
 
        if (activeTenants.Count == 0)
        {
            return DateTime.UtcNow.AddHours(1);
        }
        
        var now = DateTime.UtcNow;
        DateTime? nextAlert = null;
 
        foreach (var tenant in activeTenants)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.Timezone);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
            
            
            var driverAlertLocal = new DateTime(
                nowLocal.Year, nowLocal.Month, nowLocal.Day,
                tenant.AlertDriverHour, 0, 0);
 
            if (driverAlertLocal <= nowLocal)
            {
                driverAlertLocal = driverAlertLocal.AddDays(1);
            }
 
            var driverAlertUtc = TimeZoneInfo.ConvertTimeToUtc(driverAlertLocal, timeZone);
 
            if (nextAlert == null || driverAlertUtc < nextAlert)
            {
                nextAlert = driverAlertUtc;
            }
 
            var managerAlertLocal = new DateTime(
                nowLocal.Year, nowLocal.Month, nowLocal.Day,
                tenant.AlertManagerHour, 0, 0);
 
            if (managerAlertLocal <= nowLocal)
            {
                managerAlertLocal = managerAlertLocal.AddDays(1);
            }
 
            var managerAlertUtc = TimeZoneInfo.ConvertTimeToUtc(managerAlertLocal, timeZone);
 
            if (nextAlert == null || managerAlertUtc < nextAlert)
            {
                nextAlert = managerAlertUtc;
            }
        }
        
        return nextAlert ?? DateTime.UtcNow.AddHours(1);
    }
    
    /// <summary>
    /// Cria escopo de DI, carrega tenants e dispara os alertas quando o horário bate.
    /// Controla disparos duplicados com dicionários em memória indexados por tenant.
    /// </summary>
    private async Task RunCheckAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tenantRepository = scope.ServiceProvider.GetRequiredService<TenantRepository>();
            var complianceService = scope.ServiceProvider.GetRequiredService<DailyLogComplianceService>();
            var alertService = scope.ServiceProvider.GetRequiredService<WhatsAppAlertService>();
            var activeTenants = tenantRepository.GetAllActive();
 
            foreach (var tenant in activeTenants)
            {
                try
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.Timezone);
                    var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                    var todayInTenant = DateOnly.FromDateTime(nowLocal);
                    int currentHour = nowLocal.Hour;
                    
                    if (nowLocal.DayOfWeek == DayOfWeek.Saturday || nowLocal.DayOfWeek == DayOfWeek.Sunday)
                    {
                        _logger.LogInformation(
                            "WhatsAppNotificationJob: skipping tenant {TenantId} — weekend.", tenant.Id);
                        continue;
                    }
 
                    string driverKey = string.Format("{0}_driver", tenant.Id);
                    string managerKey = string.Format("{0}_manager", tenant.Id);
 
                    bool driverAlertDue = currentHour == tenant.AlertDriverHour
                                          && (!_lastDriverAlertDate.TryGetValue(driverKey, out var lastDriver)
                                              || lastDriver < todayInTenant);
 
                    bool managerAlertDue = currentHour == tenant.AlertManagerHour
                                           && (!_lastManagerAlertDate.TryGetValue(managerKey, out var lastManager)
                                               || lastManager < todayInTenant);
 
                    if (driverAlertDue)
                    {
                        _logger.LogInformation(
                            "WhatsAppNotificationJob: firing driver alerts for tenant {TenantId}.",
                            tenant.Id);
                        
                        var pendingDriver = complianceService.GetPendingDrivers(tenant.Id, todayInTenant);
 
                        if (pendingDriver.Count > 0)
                        {
                            await alertService.SendDriverAlertsAsync(tenant.Id, pendingDriver);
                        }
                        
                        _lastDriverAlertDate[driverKey] = todayInTenant;
                    }
                    
                    
                    if (managerAlertDue)
                    {
                        var pendingDrivers = complianceService.GetPendingDrivers(tenant.Id, todayInTenant);
                        
                        if (pendingDrivers.Count > 0)
                        {
                            await alertService.SendManagerSummaryAsync(tenant.Id, tenant, pendingDrivers);
                        }
                        
                        _lastManagerAlertDate[managerKey] = todayInTenant;
                    }
                    
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "WhatsAppNotificationJob: error on tenant {TenantId}.", tenant.Id);
                }
                
            }
 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WhatsAppNotificationJob: error during check execution.");
        }
    }
 
}