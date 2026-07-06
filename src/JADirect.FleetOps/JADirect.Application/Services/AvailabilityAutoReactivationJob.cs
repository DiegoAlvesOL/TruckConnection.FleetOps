using JADirect.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;

/// <summary>
/// BackgroundService que executa automaticamente à meia-noite para processar
/// períodos de indisponibilidade de motoristas.
/// Fluxo:
/// 1. Inicia quando a aplicação sobe
/// 2. Calcula tempo até próxima meia-noite (00:01 UTC)
/// 3. Aguarda dormindo (sem bloquear threads)
/// 4. À meia-noite, para cada tenant ativo:
///    - Marca drivers cujas férias começam hoje como OnLeave/Sick
///    - Reativa drivers cujas férias terminam hoje (volta para Active)
/// 5. Volta ao passo 2
/// Sem este job, o manager teria que atualizar manualmente o status
/// de cada driver todos os dias quando férias começam/terminam.
/// </summary>

public class AvailabilityAutoReactivationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AvailabilityAutoReactivationJob> _logger;

    /// <summary>
    /// Inicializa o background job com injeção de dependências.
    /// </summary>
    /// <param name="serviceProvider">Provedor de serviços para obter repositórios dentro do job.</param>
    /// <param name="logger">Logger estruturado para auditoria e debugging.</param>
    public AvailabilityAutoReactivationJob(
        IServiceProvider serviceProvider,
        ILogger<AvailabilityAutoReactivationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Método principal do BackgroundService. Executa um loop infinito que:
    /// - Calcula tempo até meia-noite
    /// - Aguarda de forma assíncrona (não bloqueia threads)
    /// - Processa períodos quando meia-noite chega
    /// - Repete indefinidamente até a aplicação desligar
    /// </summary>
    /// <param name="stoppingToken">Token que sinaliza quando a aplicação está desligando.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AvailabilityAutoReactivationJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calcula tempo até meia noite (00:01 UTC)
                TimeSpan timeUntilMidnight = CalculateTimeUntilMidnight();

                _logger.LogInformation(
                    "AvailabilityAutoReactivationJob: next run in {Hours}h {Minutes}m.",
                    timeUntilMidnight.Hours, timeUntilMidnight.Minutes);

                // Aguarda até meia noite (de forma assíncrona)
                await Task.Delay(timeUntilMidnight, stoppingToken);

                // Executar o processamento
                await ProcessAvailabilityPeriods(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AvailabilityAutoReactivationJob stopped");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AvailabilityAutoReactivationJob: Error on loop");
            }
        }
    }

    /// <summary>
    /// Calcula quanto tempo falta até a próxima meia-noite (00:01 UTC).
    /// 
    /// Exemplo:
    /// - Agora: 14:30 UTC
    /// - Próxima meia-noite: 00:01 UTC de amanhã
    /// - Retorna: 9h 31m
    /// 
    /// Se o cálculo resultar em menos de 1 minuto (caso raro), retorna 1 minuto
    /// por segurança (evita um delay de 0 que causaria loop infinito).
    /// </summary>
    /// <returns>TimeSpan com horas e minutos até meia-noite.</returns>
    private TimeSpan CalculateTimeUntilMidnight()
    {
        DateTime utcNow = DateTime.UtcNow;
        
        // Proxima meia noite: 00:01 UTC do proximo dia
        DateTime nextMidnight = utcNow.Date.AddDays(1).AddMinutes(1);
        
        TimeSpan timeUntill = nextMidnight - utcNow;

        // Segurança: nunca retornar delay < 1 minuto
        if (timeUntill.TotalSeconds < 60)
        {
            timeUntill = TimeSpan.FromMinutes(1);
        }
        return timeUntill;
    }

    /// <summary>
    /// Processa períodos de indisponibilidade para todos os tenants ativos.
    /// 
    /// Fluxo:
    /// 1. Cria um novo scope de DI (necessário porque BackgroundService não tem scope automático)
    /// 2. Obtém TenantRepository e AvailabilityService do scope
    /// 3. Busca todos os tenants ativos
    /// 4. Para cada tenant:
    ///    - Chama ProcessStartingAvailabilityPeriods() (marca como indisponível)
    ///    - Chama ProcessExpiredAvailabilityPeriods() (reativa motorista)
    /// 5. Se um tenant falhar, loga erro e continua com próximo (resilência)
    /// 
    /// Estratégia de erro: Falha parcial é melhor que parada completa.
    /// Se tenant A falhar, tenant B ainda é processado.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelamento (para respeitar shutdown da app).</param>
    private async Task ProcessAvailabilityPeriods(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AvailabilityAutoReactivationJob: processing periods.");

        try
        {
            // Cria um novo scope (BackgroundServices não têm scope automático)
            // Sem isso, não consegue acessar repositórios registrados como Scoped
            using var scope = _serviceProvider.CreateScope();
            var tenantRepository = scope.ServiceProvider.GetRequiredService<TenantRepository>();
            var availabilityService = scope.ServiceProvider.GetRequiredService<AvailabilityService>();
            
            // Obtém todos os tenants ativos
            var activeTenants = tenantRepository.GetAllActive()
                .Where(tenant => tenant.IsActive)
                .ToList();

            if (activeTenants.Count == 0)
            {
                _logger.LogWarning("AvailabilityAutoReactivationJob: no active tenants found.");
                return;
            }
            
            // Processa cada tenant independentemente
            foreach (var tenant in activeTenants)
            {
                try
                {
                    // 1. Processa períodos que INICIAM hoje
                    // (marca drivers como OnLeave/Sick)
                    availabilityService.ProcessStartingAvailabilityPeriods(tenant.Id);
                    
                    // 2. Processa períodos que EXPIRAM hoje
                    // (reativa drivers para Active)
                    availabilityService.ProcessExpiredAvailabilityPeriods(tenant.Id);
                    
                    _logger.LogInformation(
                        "AvailabilityAutoReactivationJob: tenant {TenantId} processed.",
                        tenant.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AvailabilityAutoReactivationJob: error whilst processing tenant {TenantId}.",
                        tenant.Id);
                    // Continua com próximo tenant (não relança exceção)
                }
            }
            
            _logger.LogInformation("AvailabilityAutoReactivationJob: processing completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AvailabilityAutoReactivationJob: Critical error.");
        }
        
        await Task.CompletedTask;
    }
}