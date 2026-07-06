using System.Text.Json;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;

namespace JADirect.Application.Services;

/// <summary>
/// Orquestrador das regras de negócio do Walkaround Check.
/// </summary>
public class WalkaroundService
{
    private readonly InspectionRepository _inspectionRepository;
    private readonly BlockingRuleRepository _blockingRuleRepository;
    private readonly AssignmentService _assignmentService;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Inicializa o serviço com os repositórios e serviços necessários via injeção de dependência.
    /// </summary>
    public WalkaroundService(
        InspectionRepository inspectionRepository,
        BlockingRuleRepository blockingRuleRepository,
        AssignmentService assignmentService)
    {
        _inspectionRepository    = inspectionRepository;
        _blockingRuleRepository  = blockingRuleRepository;
        _assignmentService       = assignmentService;
    }

    /// <summary>
    /// Processa a submissão direta de um walkaround check sem fluxo de draft.
    /// Mantido para compatibilidade. Popula assignment_id automaticamente.
    /// </summary>
    public (bool VehicleBlocked, string ErrorMessage) SubmitInspection(
        int userId,
        int vehicleId,
        int tenantId,
        int odometer,
        List<ChecklistItemResult> items,
        decimal? latitude,
        decimal? longitude)
    {
        bool allItemsAnswered = items.All(item => !string.IsNullOrEmpty(item.State));

        if (!allItemsAnswered)
        {
            return (false, "All checklist items must be answered before submitting.");
        }

        bool allActionsSelected = items
            .Where(item => item.State == "Attention" || item.State == "Defect")
            .All(item => !string.IsNullOrEmpty(item.ActionTaken) && item.ActionTaken != "None");

        if (!allActionsSelected)
        {
            return (false, "All flagged items must have an action selected before submitting.");
        }

        var blockingRules = _blockingRuleRepository.GetRulesByTenant(tenantId);

        bool vehicleBlocked = items.Any(item =>
        {
            if (item.State == "Good") { return false; }
            return blockingRules.Any(rule =>
                rule.ItemState == item.State &&
                rule.ActionTaken == item.ActionTaken &&
                rule.BlocksVehicle);
        });

        string checklistJson  = JsonSerializer.Serialize(items, JsonOptions);
        int vehicleStatusId   = vehicleBlocked ? 4 : 1;

        // Consulta o assignment ativo para rastreabilidade.
        var activeAssignment = _assignmentService.GetTodayJourneyState(userId);

        var walkaroundCheck = new WalkaroundCheck
        {
            UserId       = userId,
            VehicleId    = vehicleId,
            AssignmentId = activeAssignment?.Id,
            Odometer     = odometer,
            HasDefect    = vehicleBlocked,
            ChecklistJson = checklistJson,
            Latitude     = latitude,
            Longitude    = longitude
        };

        _inspectionRepository.Add(walkaroundCheck, vehicleStatusId);

        return (vehicleBlocked, string.Empty);
    }

    /// <summary>
    /// Cria um registro de walkaround check com status Draft.
    /// Popula assignment_id automaticamente consultando o assignment ativo do motorista.
    /// </summary>
    public int StartDraft(
        int userId,
        int vehicleId,
        int odometer,
        decimal? latitude,
        decimal? longitude)
    {
        // Consulta o assignment ativo para rastreabilidade.
        var activeAssignment = _assignmentService.GetTodayJourneyState(userId);

        var draft = new WalkaroundCheck
        {
            UserId       = userId,
            VehicleId    = vehicleId,
            AssignmentId = activeAssignment?.Id,
            Odometer     = odometer,
            HasDefect    = false,
            ChecklistJson = "[]",
            Status       = WalkaroundCheckStatus.Draft,
            Latitude     = latitude,
            Longitude    = longitude
        };

        return _inspectionRepository.CreateDraft(draft);
    }

    /// <summary>
    /// Conclui um walkaround check em estado Draft aplicando todas as regras de negócio.
    /// </summary>
    public (bool VehicleBlocked, bool Success, string ErrorMessage) FinalizeDraft(
        int walkaroundCheckId,
        int vehicleId,
        int tenantId,
        List<ChecklistItemResult> items,
        int odometer,
        decimal? latitude,
        decimal? longitude)
    {
        bool allItemsAnswered = items.All(item => !string.IsNullOrEmpty(item.State));

        if (!allItemsAnswered)
        {
            return (false, false, "All checklist items must be answered before submitting.");
        }

        bool allActionsSelected = items
            .Where(item => item.State == "Attention" || item.State == "Defect")
            .All(item => !string.IsNullOrEmpty(item.ActionTaken) && item.ActionTaken != "None");

        if (!allActionsSelected)
        {
            return (false, false, "All flagged items must have an action selected before submitting.");
        }

        var blockingRules = _blockingRuleRepository.GetRulesByTenant(tenantId);

        bool vehicleBlocked = items.Any(item =>
        {
            if (item.State == "Good") { return false; }
            return blockingRules.Any(rule =>
                rule.ItemState == item.State &&
                rule.ActionTaken == item.ActionTaken &&
                rule.BlocksVehicle);
        });

        string checklistJson = JsonSerializer.Serialize(items, JsonOptions);
        int vehicleStatusId  = vehicleBlocked ? 4 : 1;

        bool completed = _inspectionRepository.CompleteDraft(
            walkaroundCheckId,
            checklistJson,
            vehicleBlocked,
            vehicleStatusId,
            vehicleId,
            odometer,
            latitude,
            longitude);

        if (!completed)
        {
            return (false, false, "Walkaround draft not found or already completed.");
        }

        return (vehicleBlocked, true, string.Empty);
    }
}