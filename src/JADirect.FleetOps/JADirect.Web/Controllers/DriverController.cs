using JADirect.Data.Repositories;
using JADirect.Application.Services;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;

/// <summary>
/// Gerencia o Dashboard do motorista. 
/// Seu papel é orquestrador, delegando de acordo com cada serviços.
/// </summary>
[Authorize]
public class DriverController : Controller
{
    private readonly VehicleRepository _vehicleRepository;
    private readonly DailyLogRepository _dailyLogRepository;
    private readonly FleetService _fleetService;
    private readonly AssignmentService _assignmentService;

    /// <summary>
    /// Injeção de dependências. Note que não precisamos mais do InspectionRepository aqui,
    /// pois o FleetService usa a data já mapeada no objeto Vehicle.
    /// </summary>
    public DriverController(VehicleRepository vehicleRepository, 
                            DailyLogRepository dailyLogRepository,
                            FleetService fleetService, AssignmentService assignmentService)
    {
        _vehicleRepository = vehicleRepository;
        _dailyLogRepository = dailyLogRepository;
        _fleetService = fleetService;
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Carrega o Dashboard principal.
    /// Utiliza o FleetService para aplicar as regras de Van vs Caminhão em cada veículo.
    /// </summary>
    [HttpGet]
    public IActionResult SelectVehicle()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("Login", "Account");
        }

        int userId = int.Parse(userIdClaim);
        var dashboardData = new DriverDashboardModel();
        var activeAssignment = _assignmentService.GetTodayJourneyState(userId);

        if (activeAssignment == null)
        {
            dashboardData.JourneyStep = JourneyStep.NeedsVehicle;
        }
        else
        {
            dashboardData.ActiveAssignment = activeAssignment;

            var vehicle = _vehicleRepository.GetById(activeAssignment.VehicleId);

            if (vehicle == null)
            {
                dashboardData.JourneyStep = JourneyStep.NeedsWalkaround;
            }
            else
            {
                var compliance = _fleetService.GetVehicleCompliance(
                    vehicle.Id,
                    vehicle.RegistrationNo,
                    vehicle.Manufacturer,
                    vehicle.Model,
                    vehicle.CurrentKm,
                    vehicle.LastWalkaroundAt,
                    vehicle.VehicleType,
                    (int)vehicle.Status
                );
                
                dashboardData.VehicleCompliance = compliance;

                if (compliance.IsDailyLogAllowed)
                {
                    dashboardData.HasWalkaroundToday = true;
                    dashboardData.JourneyStep = JourneyStep.Ready;
                }
                else
                {
                    dashboardData.HasWalkaroundToday = false;
                    dashboardData.JourneyStep = JourneyStep.NeedsWalkaround;
                }
            }
        }

        dashboardData.RecentActivities = _dailyLogRepository.GetRecentLogs(userId);

        return View(dashboardData);
    }

    /// <summary>
    /// Recebe a seleção do veículo e a intenção (Daily Log ou Walkaround).
    /// Salva os dados na sessão para uso nos próximos passos.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmVehicle(int vehicleId, string registrationNo, string actionType)
    {
        if (vehicleId <= 0 || string.IsNullOrEmpty(registrationNo))
        {
            return RedirectToAction("SelectVehicle");
        }

        // Armazena o veículo escolhido na sessão para persistência entre telas
        HttpContext.Session.SetInt32("SelectedVehicleId", vehicleId);
        HttpContext.Session.SetString("SelectedVehicleRegistrationNo", registrationNo);
        
        // Redirecionamento baseado na ação escolhida na View
        if (actionType == "DailyLog")
        {
            return RedirectToAction("Create", "DailyLog");
        }
            
        return RedirectToAction("Create", "Walkaround");
    }
}