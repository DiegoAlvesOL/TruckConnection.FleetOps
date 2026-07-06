using JADirect.Application.Services;
using JADirect.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;


/// <summary>
/// Controller responsável pelo fluxo de alocação diária de motorista a veículo.
/// Todos os endpoints exigem autenticação. O driverId é sempre extraído dos Claims,
/// nunca recebido como parâmetro externo, para evitar adulteração de identidade.
/// </summary>
[Authorize]
public class AssignmentController : Controller
{
    private readonly AssignmentService _assignmentService;
    private readonly VehicleRepository _vehicleRepository;

    public AssignmentController(AssignmentService assignmentService, VehicleRepository vehicleRepository)
    {
        _assignmentService = assignmentService;
        _vehicleRepository = vehicleRepository;
    }

    /// <summary>
    /// Processa a solicitação de assumir um veículo para o dia atual.
    /// Extrai o driverId dos Claims e delega as validações ao AssignmentService.
    /// Em caso de sucesso, redireciona para o dashboard do motorista.
    /// Em caso de conflito, exibe mensagem de erro via TempData na mesma tela.
    /// </summary>
    /// <param name="vehicleId">
    /// ID do veículo a ser assumido. Na Fase 2, este parâmetro também poderá ser
    /// resolvido a partir da placa digitada ou lida via QR code, com consulta ao VehicleRepository.
    /// </param>
    [HttpGet]
    public IActionResult Assume()
    {
        return View();
    }

    /// <summary>
    /// Processa a solicitação de assumir um veículo a partir da placa digitada pelo motorista.
    /// Traduz a placa para vehicleId via repositório e delega as validações ao AssignmentService.
    /// Em caso de sucesso, redireciona para o dashboard do motorista.
    /// Em caso de erro, exibe mensagem via TempData na mesma tela.
    /// </summary>
    /// <param name="registrationNo">Placa digitada pelo motorista no formulário.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Assume(string registrationNo)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("Login", "Account");
        }

        // PASSO 1: Validar que a placa foi informada.
        if (string.IsNullOrWhiteSpace(registrationNo))
        {
            TempData["ErrorMessage"] = "Please enter a vehicle registration number.";
            return RedirectToAction(nameof(Assume));
        }
        string normalizedRegistrationNo = registrationNo
            .Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpper();

        // PASSO 2: Traduzir a placa para a entidade Vehicle.
        // O motorista digita texto legível. O serviço opera com vehicleId.
        var vehicle = _vehicleRepository.GetByRegistrationNo(normalizedRegistrationNo);

        if (vehicle == null)
        {
            TempData["ErrorMessage"] = $"No vehicle found with registration '{registrationNo.Trim().ToUpper()}'. Please check and try again.";
            return RedirectToAction(nameof(Assume));
        }

        // PASSO 3: Verificar se o veículo está operacional.
        // Veículos bloqueados ou inativos não podem ser assumidos.
        if ((int)vehicle.Status != 1)
        {
            TempData["ErrorMessage"] = $"Vehicle {vehicle.RegistrationNo} is not available for assignment. Please contact your manager.";
            return RedirectToAction(nameof(Assume));
        }

        // PASSO 4: Delegar ao serviço com o vehicleId resolvido.
        int driverId = int.Parse(userIdClaim);

        var (success, errorMessage) = _assignmentService.AssignVehicleToDriver(driverId, vehicle.Id);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction(nameof(Assume));
        }

        TempData["SuccessMessage"] = $"Vehicle {vehicle.RegistrationNo} assumed successfully. Have a safe day!";
        return RedirectToAction("SelectVehicle", "Driver");
    }
    
    
    /// <summary>
    /// Processa a solicitação de devolução do veículo ativo do motorista.
    /// Permitido a qualquer momento do dia, independentemente do walkaround check.
    /// O walkaround pertence ao veículo e permanece válido para o próximo motorista.
    /// Em caso de sucesso, redireciona para a tela de assumir van.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unassign()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("Login", "Account");
        }

        int driverId = int.Parse(userIdClaim);

        var (success, errorMessage) = _assignmentService.UnassignVehicle(driverId);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction("SelectVehicle", "Driver");
        }

        TempData["SuccessMessage"] = "Vehicle returned successfully. You can assume a new vehicle when ready.";
        return RedirectToAction(nameof(Assume));
    }


    /// <summary>
    /// Retorna o estado atual da jornada do motorista autenticado em formato JSON.
    /// Consumido via fetch pelo dashboard do motorista na Fase 2.
    /// Retorna 200 com os dados do assignment se existir, ou 200 com null se não houver jornada ativa.
    /// </summary>
    [HttpGet]
    public IActionResult State()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        int driverId = int.Parse(userIdClaim);
        var assignment = _assignmentService.GetTodayJourneyState(driverId);
        return Json(assignment);
    }
}