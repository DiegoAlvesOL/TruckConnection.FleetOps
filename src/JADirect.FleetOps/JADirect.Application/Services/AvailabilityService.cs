using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;

/// <summary>
/// Serviço responsável pela lógica de negócio relacionada a períodos
/// de indisponibilidade de motoristas. Valida entrada, coordena
/// repositórios e registra eventos via logging.
/// </summary>
public class AvailabilityService
{
    private readonly AvailabilityRepository _availabilityRepository;
    private readonly UserRepository _userRepository;
    private readonly ILogger<AvailabilityService> _logger;

    /// <summary>
    /// Inicializa o serviço com injeção de dependência.
    /// </summary>
    /// <param name="availabilityRepository">Repositório de períodos de indisponibilidade.</param>
    /// <param name="userRepository">Repositório de usuários (para atualizar status).</param>
    /// <param name="logger">Logger estruturado para eventos.</param>
    public AvailabilityService(
        AvailabilityRepository availabilityRepository,
        UserRepository userRepository,
        ILogger<AvailabilityService> logger)
    {
        _availabilityRepository = availabilityRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Marca um motorista como indisponível (férias ou licença) por um período específico.
    /// Valida datas, cria o período no banco e atualiza o status do motorista.
    /// </summary>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="driverId">ID do motorista a marcar como indisponível.</param>
    /// <param name="status">Status durante período: OnLeave ou Sick.</param>
    /// <param name="fromDate">Data de início da indisponibilidade.</param>
    /// <param name="toDate">Data de retorno (inclusive).</param>
    /// <param name="reason">Motivo da indisponibilidade (ex: "Vacation to Brazil").</param>
    /// <exception cref="ArgumentException">Se datas são inválidas ou status inválido.</exception>
    public void MarkUnavailable(
        int tenantId,
        int driverId,
        UserStatus status,
        DateTime fromDate,
        DateTime toDate,
        string? reason = null)
    {
        // Validação 1: Status deve ser OnLeave ou Sick
        if (status != UserStatus.OnLeave && status != UserStatus.Sick)
        {
            var errorMsg = "Only 'On Leave' and 'Sick' statuses allow unavailability dates. Please select one.";
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(status));
        }

        // Validação 2: toDate deve ser >= fromDate
        if (toDate.Date < fromDate.Date)
        {
            var errorMsg = "The return date cannot be earlier than the start date. Please check your dates.";
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(toDate));
        }

        // Validação 3: fromDate não pode ser retroativa (no máximo hoje)
        if (fromDate.Date < DateTime.Now.Date)
        {
            var errorMsg = "The start date of the unavailability cannot be in the past. Please select today or a future date.";
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(fromDate));
        }

        // Criar período no banco (status inicia como 'active')
        _availabilityRepository.Add(tenantId, driverId, status, fromDate, toDate, reason);

        _logger.LogInformation(
            "AvailabilityService: motorista {DriverId} (tenant {TenantId}) " +
            "marcado como indisponível {Status} de {FromDate:yyyy-MM-dd} a {ToDate:yyyy-MM-dd}. " +
            "Motivo: {Reason}",
            driverId, tenantId, status, fromDate.Date, toDate.Date, reason ?? "não informado");
    }

    /// <summary>
    /// Cancela um período de indisponibilidade ativo antes de sua data de retorno.
    /// Marca como 'canceled' para preservar histórico.
    /// Manager pode chamar isso quando driver volta antes do planejado.
    /// </summary>
    /// <param name="periodId">ID do período a cancelar.</param>
    public void RemoveUnavailability(int periodId)
    {
        _availabilityRepository.MarkAsCanceled(periodId);

        _logger.LogInformation(
            "AvailabilityService: período de indisponibilidade {PeriodId} cancelado.",
            periodId);
    }

    /// <summary>
    /// Verifica se um motorista pode receber alertas de conformidade em uma data específica.
    /// Retorna false se o motorista está em período de indisponibilidade na data informada.
    /// </summary>
    /// <param name="driverId">ID do motorista a verificar.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="date">Data a verificar (normalmente hoje).</param>
    /// <returns>true se driver está disponível e pode receber alertas; false se indisponível.</returns>
    public bool IsAvailableForAlerts(int driverId, int tenantId, DateOnly date)
    {
        var activePeriod = _availabilityRepository.GetActiveByDriver(driverId, tenantId, date);

        if (activePeriod != null)
        {
            _logger.LogDebug(
                "AvailabilityService: motorista {DriverId} indisponível em {Date:yyyy-MM-dd} " +
                "(período: {FromDate:yyyy-MM-dd} a {ToDate:yyyy-MM-dd}).",
                driverId, date, activePeriod.AvailabilityFromDate.Date, activePeriod.AvailabilityToDate.Date);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Processa períodos expirados no dia de hoje, reativando motoristas automaticamente.
    /// Marca período como 'expired' (não deleta mais).
    /// Chamado diariamente por AvailabilityAutoReactivationJob.
    /// </summary>
    /// <param name="tenantId">ID do tenant cujos períodos vencidos devem ser processados.</param>
    public void ProcessExpiredAvailabilityPeriods(int tenantId)
    {
        var expiredPeriods = _availabilityRepository.GetAllExpiredToday(tenantId);

        if (expiredPeriods.Count == 0)
        {
            _logger.LogDebug(
                "AvailabilityService: nenhum período vencido hoje para tenant {TenantId}.",
                tenantId);
            return;
        }

        foreach (var period in expiredPeriods)
        {
            try
            {
                // Reativar motorista (voltar para Active)
                _userRepository.Activate(period.DriverId);

                // Marcar período como expirado (não deleta)
                _availabilityRepository.MarkAsExpired(period.Id);

                _logger.LogInformation(
                    "AvailabilityService: motorista {DriverId} reativado automaticamente. " +
                    "Período {PeriodId} ({FromDate:yyyy-MM-dd} a {ToDate:yyyy-MM-dd}) finalizado e preservado no histórico.",
                    period.DriverId, period.Id, period.AvailabilityFromDate.Date, period.AvailabilityToDate.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AvailabilityService: erro ao reativar motorista {DriverId}. " +
                    "Período {PeriodId} não foi processado.",
                    period.DriverId, period.Id);
                // Não relança exceção — permite que outros períodos sejam processados
            }
        }

        _logger.LogInformation(
            "AvailabilityService: ProcessExpiredAvailabilityPeriods completado para tenant {TenantId}. " +
            "{Count} motoristas reativados.",
            tenantId, expiredPeriods.Count);
    }

    /// <summary>
    /// Retorna o período ativo para um motorista em uma data específica.
    /// Usado pela tela Manage para carregar dados de indisponibilidade.
    /// </summary>
    /// <param name="driverId">ID do motorista.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="date">Data de referência.</param>
    public AvailabilityPeriod? GetActiveByDriver(int driverId, int tenantId, DateOnly date)
    {
        return _availabilityRepository.GetActiveByDriver(driverId, tenantId, date);
    }

    /// <summary>
    /// Retorna todos os períodos ativos (status = 'active') de um motorista,
    /// incluindo períodos futuros agendados. Usado pela tela Manage.
    /// </summary>
    /// <param name="driverId">ID do motorista.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <returns>Lista de períodos ativos.</returns>
    public List<AvailabilityPeriod> GetAllActiveByDriver(int driverId, int tenantId)
    {
        return _availabilityRepository.GetAllActiveByDriver(driverId, tenantId);
    }
    
    /// <summary>
    /// Determina qual status deve ser persistido imediatamente no usuário,
    /// com base na data de início da indisponibilidade informada.
    /// Se a data de início é futura, o status atual é mantido e a mudança
    /// real só ocorre quando o job diário processar o início do período.
    /// </summary>
    /// <param name="requestedStatus">Status escolhido no formulário pelo manager.</param>
    /// <param name="currentStatus">Status atual do motorista no banco.</param>
    /// <param name="fromDate">Data de início do período de indisponibilidade, se houver.</param>
    /// <returns>O status que deve ser gravado agora.</returns>
    public UserStatus DetermineEffectiveStatus(UserStatus requestedStatus, UserStatus currentStatus, DateTime? fromDate)
    {
        var isUnavailabilityStatus = requestedStatus == UserStatus.OnLeave || requestedStatus == UserStatus.Sick;
        var startsInTheFuture = fromDate.HasValue && fromDate.Value.Date > DateTime.Now.Date;

        if (isUnavailabilityStatus && startsInTheFuture)
        {
            _logger.LogInformation(
                "AvailabilityService: status {RequestedStatus} agendado para {FromDate:yyyy-MM-dd}, " +
                "status atual {CurrentStatus} mantido até a data de início.",
                requestedStatus, fromDate.Value.Date, currentStatus);

            return currentStatus;
        }

        return requestedStatus;
    }
    

    /// <summary>
    /// Edita as datas de um período de indisponibilidade já existente.
    /// Valida consistência das datas e ausência de sobreposição com
    /// outros períodos ativos do mesmo motorista.
    /// </summary>
    /// <param name="periodId">ID do período a editar.</param>
    /// <param name="driverId">ID do motorista dono do período.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="fromDate">Nova data de início.</param>
    /// <param name="toDate">Nova data de retorno.</param>
    /// <param name="reason">Novo motivo (opcional).</param>
    /// <exception cref="ArgumentException">Se as datas são inválidas ou há sobreposição.</exception>
    public void UpdateLeave(int periodId, int driverId, int tenantId, DateTime fromDate, DateTime toDate,
        string? reason)
    {
        if (toDate.Date < fromDate.Date)
        {
            var errorMsg = "The return date cannot be earlier than the start date. Please check your dates.";
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(toDate));
        }

        var overlapping = _availabilityRepository.GetOverlappingActivePeriod(
            driverId, tenantId, fromDate, toDate, excludePeriodId: periodId);

        if (overlapping != null)
        {
            var errorMsg = string.Format(
                "These dates overlap with another active period ({0:dd MMM yyyy} to {1:dd MMM yyyy}).",
                overlapping.AvailabilityFromDate, overlapping.AvailabilityToDate);
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(fromDate));
        }

        _availabilityRepository.UpdateDates(periodId, fromDate, toDate, reason);

        _logger.LogInformation(
            "AvailabilityService: período {PeriodId} do motorista {DriverId} atualizado para {FromDate:yyyy-MM-dd} a {ToDate:yyyy-MM-dd}.",
            periodId, driverId, fromDate.Date, toDate.Date);
    }

    /// <summary>
    /// Cancela um período de indisponibilidade ativo. Se o período estiver
    /// vigente hoje e o motorista ainda estiver com o status correspondente,
    /// reativa o motorista imediatamente.
    /// </summary>
    /// <param name="periodId">ID do período a cancelar.</param>
    /// <exception cref="ArgumentException">Se o período não for encontrado.</exception>
    public void CancelLeave(int periodId)
    {
        var period = _availabilityRepository.GetById(periodId);

        if (period == null)
        {
            var errorMsg = "Availability period not found.";
            _logger.LogError(errorMsg);
            throw new ArgumentException(errorMsg, nameof(periodId));
        }

        _availabilityRepository.MarkAsCanceled(periodId);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var periodIsCurrentlyActive = DateOnly.FromDateTime(period.AvailabilityFromDate) <= today &&
            DateOnly.FromDateTime(period.AvailabilityToDate) >= today;

        if (periodIsCurrentlyActive)
        {
            var driver = _userRepository.GetById(period.DriverId);

            if (driver != null && driver.Status == period.StatusDuringPeriod)
            {
                _userRepository.Activate(period.DriverId);

                _logger.LogInformation(
                    "AvailabilityService: motorista {DriverId} reativado após cancelamento do período {PeriodId}.",
                    period.DriverId, periodId);
            }
        }

        _logger.LogInformation(
            "AvailabilityService: período {PeriodId} cancelado pelo manager.",
            periodId);
    }

    /// <summary>
    /// Processa períodos que INICIAM hoje, marcando drivers como indisponíveis.
    /// Chamado diariamente por AvailabilityAutoReactivationJob.
    /// </summary>
    /// <param name="tenantId">ID do tenant.</param>
    public void ProcessStartingAvailabilityPeriods(int tenantId)
    {
        var startingPeriods = _availabilityRepository.GetAllStartingToday(tenantId);

        if (startingPeriods.Count == 0)
        {
            _logger.LogDebug(
                "AvailabilityService: no periods starting today for tenant {TenantId}.", tenantId);
            return;
        }

        foreach (var period in startingPeriods)
        {
            try
            {
                var driver = _userRepository.GetById(period.DriverId);

                if (driver == null)
                {
                    _logger.LogWarning(
                        "AvailabilityService: driver {DriverId} not found.", period.DriverId);
                    continue;
                }

                // Só entra nessa parte se o driver estiver ativo (status = 1)
                if (driver.Status == UserStatus.Active)
                {
                    _userRepository.UpdateStatus(period.DriverId, period.StatusDuringPeriod);

                    _logger.LogInformation(
                        "AvailabilityService: Driver {DriverId} marked as {Status}. " +
                        "Period {PeriodId} ({FromDate:yyyy-MM-dd} a {ToDate:yyyy-MM-dd}) has started.",
                        period.DriverId, period.StatusDuringPeriod, period.Id,
                        period.AvailabilityFromDate.Date, period.AvailabilityToDate.Date);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AvailabilityService: error whilst processing driver {DriverId} for the period starting.",
                    period.DriverId);
            }
        }

        _logger.LogInformation(
            "AvailabilityService: ProcessStartingAvailabilityPeriods has been completed for tenant {TenantId}. " +
            "{Count} drivers marked as unavailable.",
            tenantId, startingPeriods.Count);
    }
}