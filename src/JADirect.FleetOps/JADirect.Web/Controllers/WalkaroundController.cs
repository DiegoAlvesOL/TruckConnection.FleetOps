using JADirect.Application.Interfaces;
using JADirect.Application.Services;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JADirect.Application.Documents;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controller responsável pelo walkaround check.
/// O fluxo é: GET Create (cria draft) → fotos incrementais → POST Create (finaliza draft).
/// O walkaroundCheckId é armazenado na sessão entre o GET e o POST.
/// </summary>
[Authorize]
public class WalkaroundController : Controller
{
    private readonly WalkaroundService _walkaroundService;
    private readonly ChecklistItemRepository _checklistItemRepository;
    private readonly InspectionRepository _inspectionRepository;
    private readonly VehicleRepository _vehicleRepository;
    private readonly PhotoService _photoService;
    private readonly PhotoRepository _photoRepository;
    private readonly IWalkaroundPdfService _walkaroundPdfService;

    private const int JaDirectTenantId = 1;
    private const int MaxPhotoFileSizeBytes = 5 * 1024 * 1024;
    private const string SessionKeyWalkaroundId = "ActiveWalkaroundId";

    /// <summary>
    /// Inicializa o controller com as dependências via injeção de dependência.
    /// </summary>
    public WalkaroundController(
        WalkaroundService walkaroundService,
        ChecklistItemRepository checklistItemRepository,
        InspectionRepository inspectionRepository,
        VehicleRepository vehicleRepository,
        PhotoService photoService,
        PhotoRepository photoRepository,
        IWalkaroundPdfService walkaroundPdfService)
    {
        _walkaroundService = walkaroundService;
        _checklistItemRepository = checklistItemRepository;
        _inspectionRepository = inspectionRepository;
        _vehicleRepository = vehicleRepository;
        _photoService = photoService;
        _photoRepository = photoRepository;
        _walkaroundPdfService = walkaroundPdfService;
    }
    
    
    /// <summary>
    /// Exibe o formulário de walkaround e cria um registro Draft no banco.
    /// O vehicleId é lido do assignment ativo, não da sessão, pois o novo fluxo
    /// não passa por ConfirmVehicle. A sessão SelectedVehicleId é gravada aqui
    /// para que o POST e o UploadPhoto possam continuar funcionando.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult Create()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        int userId = int.Parse(userIdClaim);
        
        int? vehicleId = HttpContext.Session.GetInt32("SelectedVehicleId");

        if (!vehicleId.HasValue)
        {
            // Tenta resolver pelo assignment ativo se a sessão estiver vazia.
            var assignmentService = HttpContext.RequestServices
                .GetRequiredService<AssignmentService>();

            var activeAssignment = assignmentService.GetTodayJourneyState(userId);

            if (activeAssignment == null)
            {
                return RedirectToAction("SelectVehicle", "Driver");
            }

            vehicleId = activeAssignment.VehicleId;

            // Grava na sessão para que POST e UploadPhoto continuem funcionando.
            HttpContext.Session.SetInt32("SelectedVehicleId", vehicleId.Value);
        }

        var vehicle = _vehicleRepository.GetById(vehicleId.Value);

        if (vehicle == null)
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        int draftId = _walkaroundService.StartDraft(
            userId,
            vehicleId.Value,
            odometer: 0,
            latitude: null,
            longitude: null);

        HttpContext.Session.SetInt32(SessionKeyWalkaroundId, draftId);

        int vehicleTypeId = (int)vehicle.VehicleType;
        var checklistItems = _checklistItemRepository
            .GetItemsByVehicleType(JaDirectTenantId, vehicleTypeId);

        return View(checklistItems);
    }

    /// <summary>
    /// Finaliza o walkaround check promovendo o draft para Completed.
    /// Lê o walkaroundCheckId da sessão, aplica as regras de negócio via WalkaroundService
    /// e limpa as chaves de sessão relacionadas ao walkaround após a conclusão.
    /// </summary>
    /// <param name="items">Lista de resultados dos itens preenchidos pelo motorista.</param>
    /// <param name="odometer">Leitura do odômetro informada no formulário.</param>
    /// <param name="latitude">Latitude capturada via GPS. Pode ser nula.</param>
    /// <param name="longitude">Longitude capturada via GPS. Pode ser nula.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(
        List<ChecklistItemResult> items,
        int odometer,
        decimal? latitude,
        decimal? longitude)
    {
        int? walkaroundCheckId = HttpContext.Session.GetInt32(SessionKeyWalkaroundId);
        int? vehicleId = HttpContext.Session.GetInt32("SelectedVehicleId");
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (!walkaroundCheckId.HasValue || !vehicleId.HasValue || string.IsNullOrEmpty(userIdClaim))
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        var (vehicleBlocked, success, errorMessage) = _walkaroundService.FinalizeDraft(
            walkaroundCheckId.Value,
            vehicleId.Value,
            JaDirectTenantId,
            items,
            odometer,
            latitude,
            longitude);

        if (!success)
        {
            var vehicle = _vehicleRepository.GetById(vehicleId.Value);
            int vehicleTypeId = vehicle != null ? (int)vehicle.VehicleType : 1;
            var checklistItems = _checklistItemRepository
                .GetItemsByVehicleType(JaDirectTenantId, vehicleTypeId);

            ModelState.AddModelError("", errorMessage);
            return View(checklistItems);
        }

        // Limpa as chaves de sessão relacionadas ao walkaround após conclusão bem-sucedida.
        HttpContext.Session.Remove(SessionKeyWalkaroundId);
        HttpContext.Session.Remove("SelectedVehicleId");

        return RedirectToAction("SelectVehicle", "Driver");
    }

    /// <summary>
/// Recebe o arquivo de foto de um item do checklist, valida tipo e tamanho,
/// e delega o upload ao PhotoService.
/// Exceções do bucket são capturadas e retornadas como JSON para o frontend
/// exibir mensagem legível ao motorista sem quebrar o formulário.
/// </summary>
[HttpPost]
public IActionResult UploadPhoto(IFormFile photoFile, int checklistItemId)
{
    if (photoFile == null || photoFile.Length == 0)
    {
        return BadRequest(new { error = "No file received." });
    }

    bool isMimeTypeValid = photoFile.ContentType == "image/jpeg"
                           || photoFile.ContentType == "image/png";

    if (!isMimeTypeValid)
    {
        return BadRequest(new { error = "Only JPEG and PNG files are accepted." });
    }

    if (photoFile.Length > MaxPhotoFileSizeBytes)
    {
        return BadRequest(new { error = "File size exceeds the 5 MB limit." });
    }

    int? walkaroundId = HttpContext.Session.GetInt32(SessionKeyWalkaroundId);
    int? vehicleId    = HttpContext.Session.GetInt32("SelectedVehicleId");
    var userIdClaim   = User.FindFirst("UserId")?.Value;

    if (!walkaroundId.HasValue || !vehicleId.HasValue || string.IsNullOrEmpty(userIdClaim))
    {
        return Unauthorized();
    }

    int driverId = int.Parse(userIdClaim);

    var photo = new WalkaroundPhoto
    {
        WalkaroundId    = walkaroundId.Value,
        ChecklistItemId = checklistItemId,
        DriverId        = driverId,
        VehicleId       = vehicleId.Value
    };

    try
    {
        using var stream = photoFile.OpenReadStream();

        string storageKey = _photoService.UploadAndSave(
            photo,
            stream,
            photoFile.ContentType,
            photoFile.Length);

        return Ok(new { storageKey });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"UploadPhoto error: {ex.GetType().Name} — {ex.Message}");
        return StatusCode(500, new { error = "Photo upload failed. Please try again or continue without photo." });
    }
}

    /// <summary>
    /// Proxy autenticado para acesso às fotos armazenadas no Railway Bucket.
    /// O browser carrega as tags img com src apontando para este endpoint.
    /// Retorna 404 se o storageKey não existir no banco antes de acessar o bucket.
    /// </summary>
    /// <param name="storageKey">Chave do arquivo no Railway Bucket.</param>
    [HttpGet]
    public IActionResult Photo([FromQuery] string storageKey)
    {
        try
        {
            var stream = _photoService.GetPhotoStream(storageKey);
            return File(stream, "image/jpeg");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Exibe o histórico de inspeções. Acesso exclusivo para Managers.
    /// </summary>
    /// <param name="id">ID do veículo. Se nulo, exibe o histórico de toda a frota.</param>
    [Authorize(Roles = "Manager")]
    [HttpGet]
    public IActionResult History(int? id)
    {
        List<WalkaroundHistoryViewModel> historyData;

        if (id.HasValue)
        {
            historyData = _inspectionRepository.GetHistoryByVehicleId(id.Value);
            var vehicle = _vehicleRepository.GetById(id.Value);
            ViewBag.RegistrationNo = vehicle != null ? vehicle.RegistrationNo : "Selected Vehicle";
        }
        else
        {
            historyData = _inspectionRepository.GetAllHistory();
            ViewBag.RegistrationNo = "All Fleet Vehicles";
        }

        var walkaroundIds = historyData.Select(h => h.WalkaroundId).ToList();
        var photosByWalkaround = _photoRepository.GetPhotosByWalkaroundIds(walkaroundIds);

        foreach (var inspection in historyData)
        {
            if (photosByWalkaround.ContainsKey(inspection.WalkaroundId))
            {
                inspection.PhotosByItemId = photosByWalkaround[inspection.WalkaroundId];
            }
        }

        return View(historyData);
    }

    [HttpGet]
    public async Task<IActionResult> PrintPdf(int id)
    {
        WalkaroundDetailViewModel? detail = _inspectionRepository.GetWalkaroundById(id);

        if (detail == null)
        {
            return NotFound();
        }

        byte[] pdfBytes = await _walkaroundPdfService.GenerateAsync(id);
        
        string fileName = string.Format(
            "walkaround-WLK-{0}-{1:D6}.pdf",
            detail.CheckDate.Year,
            detail.WalkaroundId);
        
        return File(pdfBytes, "application/pdf", fileName);
    }
    
    
    [HttpGet]
    public IActionResult WalkaroundDocument(int id)
    {
        WalkaroundDetailViewModel? detail = _inspectionRepository.GetWalkaroundById(id);

        if (detail == null)
        {
            return NotFound();
        }

        var pdfData = new WalkaroundPdfData
        {
            WalkaroundId = detail.WalkaroundId,
            CheckDate = detail.CheckDate,
            DriverName = detail.DriverName,
            VehicleRegistration = detail.VehicleRegistration,
            VehicleMake = detail.VehicleMake,
            VehicleModel = detail.VehicleModel,
            VehicleType = detail.VehicleType,
            Odometer = detail.Odometer,
            HasDefect = detail.HasDefect,
            Items = detail.Items,
            Photos = new List<WalkaroundPdfPhotoData>()
        };

        var photos = _photoRepository.GetPhotosByWalkaroundId(id);


        foreach (WalkaroundPhoto photo in photos)
        {
            pdfData.Photos.Add(new WalkaroundPdfPhotoData
            {
                ChecklistItemId = photo.ChecklistItemId,
                StorageKey = photo.StorageKey,
            });
        }
        
        return View(pdfData);
    }
}