using JADirect.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;


/// <summary>
/// Serviço de background responsável pela limpeza periódica de drafts abandonados.
/// Um draft é considerado abandonado quando permanece com status='Draft'
/// por mais de 24 horas sem ser concluído pelo motorista.
/// </summary>
public class DraftCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DraftCleanupService> _logger;
    
    private static readonly TimeSpan AbandonedDraftThreshold = TimeSpan.FromHours(24);


    public DraftCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DraftCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Loop principal do serviço. Executa a limpeza imediatamente ao iniciar
    /// e depois a cada 24 horas enquanto a aplicação estiver ativa.
    /// </summary>
    /// <param name="stoppingToken">Token que sinaliza quando a aplicação está sendo encerrada.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DraftCleanupService started. Scheduled daily at 02:00 UTC.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun= now.Date.AddDays(now.Hour >= 2 ? 1 : 0).AddHours(2);
            var waitTime = nextRun - now;

            _logger.LogInformation(
                "DraftCleanupService: next run at {NextRun:yyyy-MM-dd HH:mm} UTC (in {Minutes:F0} minutes).",
                nextRun, waitTime.TotalMinutes);

            await Task.Delay(waitTime, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
            {
                await CleanupAbandonedDraftsAsync();
            }
        }

        _logger.LogInformation("DraftCleanupService stopped.");
    }


    /// <summary>
    /// Cria um escopo de DI, resolve o InspectionRepository e executa a limpeza.
    /// O escopo é descartado ao final de cada execução para liberar recursos corretamente.
    /// </summary>
    private async Task CleanupAbandonedDraftsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var inspectionRepository = scope.ServiceProvider
                .GetRequiredService<InspectionRepository>();

            var cutoff = DateTime.UtcNow.Subtract(AbandonedDraftThreshold);

            int deletedCount = await Task.Run(() =>
                inspectionRepository.DeleteAbandonedDrafts(cutoff));

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "DraftCleanupService: {Count} abandoned draft(s) removed. Cutoff: {Cutoff:yyyy-MM-dd HH:mm} UTC.",
                    deletedCount,
                    cutoff);
            }
            else
            {
                _logger.LogInformation(
                    "DraftCleanupService: no abandoned drafts found. Cutoff: {Cutoff:yyyy-MM-dd HH:mm} UTC.",
                    cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DraftCleanupService: error during cleanup execution.");
        }
    }
    
}