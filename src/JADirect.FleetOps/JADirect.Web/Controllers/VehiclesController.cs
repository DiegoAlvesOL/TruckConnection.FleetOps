using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controlador responsável pelo inventário de veículos da JA Direct.
/// Acesso restrito a usuários com perfil 'Manager'
/// </summary>
[Authorize(Roles = "Manager")]
public class VehiclesController : Controller
{
    private readonly VehicleRepository _vehiclesRepository;
    private readonly InspectionRepository _inspectionRepository;

    public VehiclesController(VehicleRepository vehiclesRepository, InspectionRepository inspectionRepository)
    {
        _vehiclesRepository = vehiclesRepository;
        _inspectionRepository = inspectionRepository;
    }
    
    /// <summary>
    /// Lista os veículos da frota permitindo filtragem por placa ou modelo.
    /// </summary>
    /// <param name="searchString">Termo de busca vindo da View.</param>
    public IActionResult Index(string? searchString)
    {
        // Alterado para passar o parâmetro de busca para o repositório
        var fleet = _vehiclesRepository.GetAllFilter(searchString);
        return View(fleet);
    }

    /// <summary>
    /// Carrega os detalhes de um veículo específico para gestão.
    /// Necessário para o link 'MANAGE' da tabela.
    /// </summary>
    [HttpGet]
    public IActionResult Manage(int id)
    {
        var vehicle = _vehiclesRepository.GetById(id);
        if (vehicle == null)
        {
            return NotFound();
        }

        var viewModel = new VehicleManageViewModel
        {
            Vehicle = vehicle,
            WalkaroundHistory = _inspectionRepository.GetHistoryByVehicleId(id)
        };
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Vehicle vehicle)
    {
        ModelState.Remove("CreatedAt");
        ModelState.Remove("Status");

        if (vehicle.CurrentKm < 0)
        {
            ModelState.AddModelError("CurrentKm", "Initial mileage cannot be negative.");
        }

        if (!ModelState.IsValid)
        {
            return View(vehicle);
        }

        // Verificação de Unicidade de Placa
        if (_vehiclesRepository.Exists(vehicle.RegistrationNo))
        {
            ModelState.AddModelError("RegistrationNo", "This registration is already in use.");
            return View(vehicle);
        }

        try
        {
            vehicle.Status = VehicleStatus.Active;
            vehicle.CreatedAt = DateTime.Now;

            _vehiclesRepository.Add(vehicle);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "Database error. Please try again.");
            return View(vehicle);
        }
    }

    /// <summary>
    /// Processa a atualização dos dados cadastrais do veículo.
    /// </summary>
    /// <param name="vehicle"></param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update([Bind(Prefix = "Vehicle")] Vehicle vehicle)
    {
        ModelState.Remove("Vehicle.RegistrationNo");
        ModelState.Remove("Vehicle.CreatedAt");

        if (!ModelState.IsValid)
        {
            var freshVehicle = _vehiclesRepository.GetById(vehicle.Id);
            freshVehicle.Manufacturer = vehicle.Manufacturer;
            freshVehicle.Model = vehicle.Model;
            freshVehicle.CurrentKm = vehicle.CurrentKm;
            freshVehicle.VehicleType = vehicle.VehicleType;
            
            var viewModel = new VehicleManageViewModel
            {
                Vehicle = freshVehicle,
                WalkaroundHistory = _inspectionRepository.GetHistoryByVehicleId(vehicle.Id)
            };
            
            return View("Manage", viewModel);
        }

        try
        {
            _vehiclesRepository.UpdateVehicleDetails(vehicle);
            TempData["SuccessMessage"] = "Vehicle details updated successfully!";
            return RedirectToAction(nameof(Manage), new { id = vehicle.Id });
        }

        catch (Exception)
        {
            TempData["ErrorMessage"] = "Database error while updating details.";
            var freshVehicle = _vehiclesRepository.GetById(vehicle.Id);
            freshVehicle.Manufacturer = vehicle.Manufacturer;
            freshVehicle.Model = vehicle.Model;
            freshVehicle.CurrentKm = vehicle.CurrentKm;
            freshVehicle.VehicleType = vehicle.VehicleType;
            
            
            var viewModel = new VehicleManageViewModel
            {
                Vehicle = freshVehicle,
                WalkaroundHistory = _inspectionRepository.GetHistoryByVehicleId(vehicle.Id)
            };
            return View("Manage", viewModel);
        }
    }


    /// <summary>
    /// Action específica para a troca de status (Active, Maintenance, etc) via painel lateral.
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="newStatus"></param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(int vehicleId, VehicleStatus newStatus)
    {
        try
        {
            _vehiclesRepository.UpdateVehicleStatus(vehicleId, newStatus);
            TempData["SuccessMessage"] = $"Vehicle status changed to {newStatus}!";
            return RedirectToAction(nameof(Manage), new { id = vehicleId });
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Failed to update vehicle status.";
            return RedirectToAction(nameof(Manage), new { id = vehicleId });
        }
    }
}