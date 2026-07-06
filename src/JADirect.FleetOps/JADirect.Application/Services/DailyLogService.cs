using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;

/// <summary>
/// Orquestrador das regras de negócio do DailyLog.
/// É o único ponto autorizado a criar um novo log diário na aplicação.
/// </summary>
public class DailyLogService
{
    private readonly DailyLogRepository _repository;
    private readonly AssignmentService _assignmentService;
    private readonly ILogger<DailyLogService> _logger;

    private const int MaximumPastDaysAllowed    = 7;
    private const int ReturnsOutlierThreshold   = 15;

    /// <summary>
    /// Inicializa o serviço com o repositório, o AssignmentService e o logger.
    /// </summary>
    public DailyLogService(
        DailyLogRepository repository,
        AssignmentService assignmentService,
        ILogger<DailyLogService> logger)
    {
        _repository        = repository;
        _assignmentService = assignmentService;
        _logger            = logger;
    }

    /// <summary>
    /// Valida as regras de negócio e persiste o daily log.
    /// Popula assignment_id automaticamente a partir do assignment ativo do motorista.
    /// </summary>
    public (bool Success, string ErrorMessage) CreateLog(DailyLog log, bool confirmedByDriver = false)
    {
        if (log.LogDate.Date > DateTime.Now.Date)
        {
            return (false, "You cannot register a log for a future date.");
        }

        int daysInThePast = (DateTime.Now.Date - log.LogDate).Days;

        if (daysInThePast > MaximumPastDaysAllowed)
        {
            return (false, $"You can only register logs up to {MaximumPastDaysAllowed} days in the past.");
        }

        bool logAlreadyExists = _repository.HasLogForDate(log.UserId, log.LogDate);

        if (logAlreadyExists)
        {
            return (false, "A log for this vehicle has already been submitted for the selected date.");
        }

        // Popula o assignment_id automaticamente. Null para registros sem assignment ativo.
        var activeAssignment = _assignmentService.GetTodayJourneyState(log.UserId);
        log.AssignmentId = activeAssignment?.Id;

        if (confirmedByDriver && log.Returns > ReturnsOutlierThreshold)
        {
            _logger.LogWarning(
                "Daily log confirmed with suspicious returns value. " +
                "UserId: {UserId}, VehicleId: {VehicleId}, AssignmentId: {AssignmentId}, " +
                "Date: {LogDate}, Returns: {Returns} (threshold: {Threshold})",
                log.UserId,
                log.VehicleId,
                log.AssignmentId,
                log.LogDate.ToString("yyyy-MM-dd"),
                log.Returns,
                ReturnsOutlierThreshold);
        }

        _repository.Add(log);

        return (true, string.Empty);
    }
}