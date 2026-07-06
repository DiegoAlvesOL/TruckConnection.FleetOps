using System.Text.Json;
using JADirect.Application.Services;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controller responsável pelo registro de métricas operacionais (Entregas/Coletas/Retornos).
/// O fluxo é: Create (entrada) → Review (confirmação) → Confirm (gravação).
/// </summary>
[Authorize]
public class DailyLogController : Controller
{
    private readonly DailyLogService _dailyLogService;
    private readonly AssignmentService _assignmentService;
    private readonly VehicleRepository _vehicleRepository;

    /// <summary>
    /// Inicializa o controller com os serviços e repositórios necessários via injeção de dependência.
    /// </summary>
    public DailyLogController(
        DailyLogService dailyLogService,
        AssignmentService assignmentService,
        VehicleRepository vehicleRepository)
    {
        _dailyLogService = dailyLogService;
        _assignmentService = assignmentService;
        _vehicleRepository = vehicleRepository;
    }

    /// <summary>
    /// Exibe o formulário de lançamento do daily log.
    /// O veículo é lido do assignment ativo do motorista, não da sessão.
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("Login", "Account");
        }

        int userId = int.Parse(userIdClaim);
        var assignment = _assignmentService.GetTodayJourneyState(userId);

        if (assignment == null)
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        var log = new DailyLog
        {
            LogDate = DateTime.Now.Date,
            VehicleId = assignment.VehicleId
        };

        return View(log);
    }

    /// <summary>
    /// Recebe os dados do formulário, valida e redireciona para a tela de confirmação.
    /// Os dados são armazenados em TempData para evitar redigitação no Review.
    /// Nenhum dado é gravado no banco nesta action.
    /// </summary>
    /// <param name="log">Objeto preenchido pelo motorista na view Create.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(DailyLog log)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("Login", "Account");
        }

        int userId = int.Parse(userIdClaim);
        var assignment = _assignmentService.GetTodayJourneyState(userId);

        if (assignment == null)
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        
        log.VehicleId = assignment.VehicleId;
        log.UserId = userId;

        ModelState.Remove("UserId");
        ModelState.Remove("VehicleId");

        if (log.Deliveries < 0 || log.Collections < 0 || log.Returns < 0)
        {
            ModelState.AddModelError("", "Operational values (Deliveries/Collections/Returns) cannot be negative.");
        }

        if (!ModelState.IsValid)
        {
            var vehicle = _vehicleRepository.GetById(assignment.VehicleId);
            ViewBag.AssignedVehicleId = assignment.VehicleId;
            ViewBag.AssignedVehicleRegistration = vehicle?.RegistrationNo ?? string.Empty;
            ViewBag.AssignedVehicleDisplay = vehicle != null
                ? $"{vehicle.Manufacturer} {vehicle.Model}"
                : string.Empty;

            return View(log);
        }

        TempData["PendingLog"] = JsonSerializer.Serialize(log);

        var vehicleForReview = _vehicleRepository.GetById(assignment.VehicleId);
        TempData["ReviewVehicleDisplay"] = vehicleForReview != null
            ? $"{vehicleForReview.Manufacturer} {vehicleForReview.Model}"
            : string.Empty;
        TempData["ReviewVehicleRegistration"] = vehicleForReview?.RegistrationNo ?? string.Empty;

        return RedirectToAction(nameof(Review));
    }

    /// <summary>
    /// Exibe o resumo dos dados para confirmação pelo motorista.
    /// Os dados são lidos do TempData e preservados para a action Confirm.
    /// Se TempData estiver vazio (acesso direto à URL), redireciona para Create.
    /// </summary>
    [HttpGet]
    public IActionResult Review()
    {
        var json = TempData["PendingLog"]?.ToString();

        if (string.IsNullOrEmpty(json))
        {
            return RedirectToAction(nameof(Create));
        }

        var log = JsonSerializer.Deserialize<DailyLog>(json);

        TempData.Keep("PendingLog");
        TempData.Keep("ReviewVehicleDisplay");
        TempData.Keep("ReviewVehicleRegistration");

        ViewBag.VehicleDisplay = TempData.Peek("ReviewVehicleDisplay")?.ToString() ?? string.Empty;
        ViewBag.VehicleRegistration = TempData.Peek("ReviewVehicleRegistration")?.ToString() ?? string.Empty;

        return View(log);
    }

    /// <summary>
    /// Efetua a gravação do daily log no banco após confirmação do motorista.
    /// Os dados são lidos do TempData armazenado na action Review.
    /// Em caso de erro de negócio, redireciona para Create com mensagem.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm(bool confirmedByDriver = false)
    {
        var json = TempData["PendingLog"]?.ToString();

        if (string.IsNullOrEmpty(json))
        {
            return RedirectToAction(nameof(Create));
        }

        var log = JsonSerializer.Deserialize<DailyLog>(json);

        var (success, errorMessage) = _dailyLogService.CreateLog(log!, confirmedByDriver);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction(nameof(Create));
        }

        TempData["SuccessMessage"] = "Daily log submitted successfully. Have a great day!";
        return RedirectToAction("SelectVehicle", "Driver");
    }
}