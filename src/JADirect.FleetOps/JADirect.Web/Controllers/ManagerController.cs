using JADirect.Data.Repositories;
using JADirect.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JADirect.Domain.Models;
using JADirect.Domain.Enums;
using ClosedXML.Excel;
using System.IO;

namespace JADirect.Web.Controllers;

/// <summary>
/// Responsável pela gestão e visualização de dados operacionais da frota.
/// Acesso restrito apenas para usuários com Role 'Manager'.
/// </summary>
[Authorize(Roles = "Manager")]
public class ManagerController : Controller
{
    private readonly DailyLogRepository _dailyLogRepository;
    private readonly FleetService _fleetService;
    private readonly DailyLogComplianceService _complianceService;
    private readonly WhatsAppAlertService _alertService;
    private readonly TenantRepository _tenantRepository;
    
    // TODO: substituir pelo tenant_id do usuário autenticado
    // quando a coluna tenant_id for adicionada à tabela users.
    private const int TenantId = 1;

    public ManagerController(
        DailyLogRepository dailyLogRepository,
        DailyLogComplianceService complianceService,
        WhatsAppAlertService alertService,
        TenantRepository tenantRepository)
    {
        _dailyLogRepository = dailyLogRepository;
        _fleetService = new FleetService();
        _complianceService  = complianceService;
        _alertService       = alertService;
        _tenantRepository = tenantRepository;
    }
    
    /// <summary>
    /// Dispara manualmente os alertas de conformidade para todos os motoristas pendentes do dia.
    /// Usado como base do botão "Remind All",  ignora regras de dia da semana por ser ação manual.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TriggerComplianceAlerts()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var pendingDrivers = _complianceService.GetPendingDrivers(TenantId, today);

        if (pendingDrivers.Count == 0)
        {
            return Json(new { success = true, message = "No pending drivers today.", sent = 0 });
        }

        var tenant = _tenantRepository.GetById(TenantId);

        if (tenant == null)
        {
            return Json(new { success = false, message = "Tenant not found." });
        }

        await _alertService.SendDriverAlertsAsync(TenantId, pendingDrivers);
        await _alertService.SendManagerSummaryAsync(TenantId, tenant, pendingDrivers);

        return Json(new
        {
            success = true,
            message = string.Format("Alerts sent for {0} driver(s).", pendingDrivers.Count),
            sent = pendingDrivers.Count,
            drivers = pendingDrivers
                .Select(d => string.Format("{0} {1}", d.FirstName, d.Surname))
                .ToList()
        });
    }
    
    /// <summary>
    /// Dispara um lembrete individual para um motorista específico.
    /// Chamado pelo botão REMIND do dashboard após confirmação do manager.
    /// Ignora regras de dia da semana: o manager tomou a decisão conscientemente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemindDriver(int driverId)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var pendingDrivers = _complianceService.GetPendingDrivers(TenantId, today);
        var driver = pendingDrivers.FirstOrDefault(d => d.UserId == driverId);
        
        if (driver == null)
            return Json(new { success = false, message = "Driver not found or already submitted log." });
        await _alertService.SendDriverAlertsAsync(TenantId, new List<PendingDriverAlert> { driver });
        return Json(new
        {
            success = true,
            message = string.Format("Reminder sent to {0} {1}.", driver.FirstName, driver.Surname)
        });
    }
    
    
    /// <summary>
    /// Renderiza o Dashboard. Acionado pela rota /Manager/Index.
    /// </summary>
    [HttpGet]
    public IActionResult Index(DateTime? start, DateTime? end, string? driverName)
    {
        DateTime startDate = start ?? DateTime.Now.AddDays(-7);
        DateTime endDate = end ?? DateTime.Now;

        var report = _dailyLogRepository.GetDashboardTotals(startDate, endDate);
        report.DriverSearch = driverName;
        
        _dailyLogRepository.FillDashboardDetails(report);
        _dailyLogRepository.FillComplianceExceptions(report); 
        
        // Recupera a lista de tuplas (Veículo + Nome do Motorista)
        var fleetData = _dailyLogRepository.GetAllVehiclesForComplianceCheck();

        foreach (var data in fleetData)
        {
            var v = data.Vehicle;
            var lastDriver = data.LastDriver;

            // ÚNICA CHAMADA: FleetService processa a regra de negócio
            var compliance = _fleetService.GetVehicleCompliance(
                v.Id, v.RegistrationNo, v.Manufacturer, v.Model, 
                v.CurrentKm, v.LastWalkaroundAt, v.VehicleType, (int)v.Status);

            // 1. Safety Alerts (Critical) -> Veículos Bloqueados (Status 4)
            if (v.Status == VehicleStatus.Blocked)
            {
                report.PendingWalkarounds.Add(new ComplianceExceptionViewModel {
                    VehicleId =  v.Id,
                    RegistrationNo = v.RegistrationNo,
                    DriverName = lastDriver,
                    Message = compliance.StatusMessage,
                    Severity = "danger"
                });
            }
            // 2. Inspection Status (Upcoming) -> Red (Expirado) ou Yellow (A vencer)
            else if (compliance.StatusColor == "Red" || compliance.StatusColor == "Yellow")
            {
                report.ExpiringInspections.Add(new ComplianceExceptionViewModel {
                    VehicleId = v.Id,
                    RegistrationNo = v.RegistrationNo,
                    DriverName = lastDriver,
                    Message = compliance.StatusMessage,
                    Severity = compliance.StatusColor == "Red" ? "danger" : "warning"
                });
            }
        }
        
        return View(report);
    }

    [HttpGet]
    public IActionResult ExportExcel(DateTime? start, DateTime? end, string? driverName)
    {

        // Se as datas vierem vazias do botão, ele assume o padrão de 7 dias
        DateTime startDate = start ?? DateTime.Now.AddDays(-7);
        DateTime endDate = end ?? DateTime.Now;
        
        // 2. Criar o ViewModel com os dados de busca
        var report = new PerformanceReportViewModel()
        {
            StartDate = startDate,
            EndDate = endDate,
            DriverSearch = driverName,
        };
        
        // Chamar o repositório para preencher a lista DetailedLogs
        // Aqui o repositório vai rodar o SQL e dar o .Add() na lista
        _dailyLogRepository.FillDashboardDetails(report);
        
        
        // Cria o arquivo Excel usando o plug in ClosedXML
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Audit Logs");
            
            
            // Estilo do Cabeçalho
            var  headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#008080");
            headerRow.Style.Font.FontColor = XLColor.White;
            
            // Cabeçalho da planilha
            worksheet.Cell(1, 1).Value = "Date";
            worksheet.Cell(1, 2).Value = "Driver";
            worksheet.Cell(1, 3).Value = "Plate";
            worksheet.Cell(1, 4).Value = "Deliveries";
            worksheet.Cell(1, 5).Value = "Collections";
            worksheet.Cell(1, 6).Value = "Returns";
            
            // Dados
            int currentRow = 2;
            foreach (var log in report.DetailedLogs)
            {
                worksheet.Cell(currentRow, 1).Value = log.LogDate.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 2).Value = log.DriverName;
                worksheet.Cell(currentRow, 3).Value = log.RegistrationNo;
                worksheet.Cell(currentRow, 4).Value = log.Deliveries;
                worksheet.Cell(currentRow, 5).Value = log.Collections;
                worksheet.Cell(currentRow, 6).Value = log.Returns;
                
                currentRow++;
            }
            
            worksheet.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"JADirect_Audit_{DateTime.Now:yyyyMMdd}.xlsx"
                );
            }
        }
        
    }
}