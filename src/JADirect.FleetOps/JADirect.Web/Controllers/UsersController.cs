using JADirect.Application.Services;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controlador responsável por gerenciar as telas de usuários.
/// Acesso restrito a usuários com perfil 'Manager'
/// </summary>
[Authorize]
public class UsersController : Controller
{
    private readonly UserRepository _userRepository;
    private readonly AvailabilityService _availabilityService;

    public UsersController(
        UserRepository userRepository,
        AvailabilityService availabilityService)
    {
        _userRepository = userRepository;
        _availabilityService = availabilityService;
    }

    /// <summary>
    /// Função que lista todos os usuário por meio da função GetAll do UserRepository.cs
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "Manager")]
    public IActionResult Index(string? searchString)
    {
        var users = _userRepository.GetAll(searchString);
        return View(users);
    }

    /// <summary>
    /// Essa ação apenas abre a tela de cadastro.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Authorize(Roles = "Manager")]
    public IActionResult Create()
    {
        return View();
    }

    /// <summary>
    /// A ação POST recebe os dados do formulário e chama a função Add no arquivos UserRespository para realizar o cadastro.
    /// O cadastro acontece apenas após a verificação se o e-mail já está cadastrado.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="plainPassord"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(User user, string plainPassword)
    {
        
        ModelState.Remove("PasswordHash");
        ModelState.Remove("CreatedAt");
        ModelState.Remove("Status");
        
        //1. Validar se o Model está consistente de acordo com as DataAnnotations da Entidade
        if (!ModelState.IsValid)
        {
            return View(user);
        }
        
        //2. Blindagem contra valores nulos antes de processar o Hash
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            ModelState.AddModelError("plainPassword", "A temporary password is required for new accounts.");
            return View(user);
        }
        
        // 3. Verificação de Unicidade: O e-mail é a chave de login, não pode ser duplicado
        var existingUser = _userRepository.GetByEmail(user.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("email", "This email address is already registered in the system.");
            return View(user);
        }

        try
        {
            // 4. Preparação da Entidade
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            user.CreatedAt = DateTime.Now;
            user.Status = UserStatus.Active;

            // 5. Persistência
            _userRepository.Add(user);

            return RedirectToAction("Index");

        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An internal error occurred while saving the user. Please try again.");
            return View(user);
        }
    }

    /// <summary>
    /// Ação que desativa o usuário.
    /// Chama o método 'Deactivate' do seu UserRepository.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }
        _userRepository.Deactivate(id);

        TempData["SuccessMessage"] = "Staff member accesss suspended.";
        return RedirectToAction("Manage", new { id = id});
    }

    /// <summary>
    /// Ação que ativa o usuário mudando seu status para 'Active'.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult Activate(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }
        
        _userRepository.Activate(id);

        TempData["SuccessMessage"] = "Staff member access has been restored.";
        return RedirectToAction("Manage", new { id = id });
    }

    
    /// <summary>
    /// Ação do manager para alterar a senha de qualquer pessoas que esqueceu a senha.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        string hash = BCrypt.Net.BCrypt.HashPassword("JADirect@2026");
        
        _userRepository.UpdatePassword(id, hash);

        TempData["SuccessMessage"] = "Password reset to 'JADirect@2026'";
        return RedirectToAction("Manage", new { id = id });
    }



    /// <summary>
    /// Carrega os detalhes de um usuário para gestão administrativa.
    /// Se o usuário estiver em período de indisponibilidade, carrega as datas.
    /// Carrega também todos os períodos ativos (vigentes ou agendados) para a tabela de gestão.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Manager")]
    public IActionResult Manage(int id)
    {
        var user = _userRepository.GetById(id);
        if (user == null)
        {
            return NotFound();
        }

        var viewModel = new UserManagementViewModel
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            Surname = user.Surname,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            Status = user.Status,
        };

        viewModel.ActivePeriods = _availabilityService.GetAllActiveByDriver(user.Id, tenantId: 1);

        // Se usuário está em OnLeave ou Sick, carregar período ativo
        if (user.Status == UserStatus.OnLeave || user.Status == UserStatus.Sick)
        {
            var period = _availabilityService.GetActiveByDriver(user.Id, tenantId: 1, DateOnly.FromDateTime(DateTime.Now));
            if (period != null)
            {
                viewModel.AvailabilityPeriodId = period.Id;
                viewModel.AvailabilityFromDate = period.AvailabilityFromDate;
                viewModel.AvailabilityToDate = period.AvailabilityToDate;
                viewModel.AvailabilityReason = period.Reason;
            }
        }

        return View(viewModel);
    }


/// <summary>
/// Processa a atualização massiva de dados iniciada pela tela Manage.
/// Se status muda para OnLeave/Sick com datas, cria período de indisponibilidade.
/// Se volta para Active, remove período anterior.
/// </summary>
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Manager")]
public IActionResult UpdateStaff(UserManagementViewModel model)
{
    if (!User.IsInRole("Manager"))
    {
        return Forbid("Manager");
    }

    // Validações de disponibilidade (duplicação segura)
    if ((model.Status == UserStatus.OnLeave || model.Status == UserStatus.Sick) &&
        (model.AvailabilityFromDate == null || model.AvailabilityToDate == null))
    {
        TempData["ErrorMessage"] = "Availability dates are required when marking as On Leave or Sick.";
        return RedirectToAction("Manage", new { id = model.UserId });
    }

    if (model.Status == UserStatus.OnLeave || model.Status == UserStatus.Sick)
    {
        if (model.AvailabilityToDate.HasValue && model.AvailabilityFromDate.HasValue &&
            model.AvailabilityToDate.Value.Date < model.AvailabilityFromDate.Value.Date)
        {
            TempData["ErrorMessage"] = "Return date cannot be before start date.";
            return RedirectToAction("Manage", new { id = model.UserId });
        }
    }

    try
    {
        // Busca o status atual do motorista antes de decidir o que gravar
        var existingUser = _userRepository.GetById(model.UserId);

        // Decide se o novo status entra em vigor agora ou fica agendado para o job diário
        var effectiveStatus = _availabilityService.DetermineEffectiveStatus(
            model.Status, existingUser.Status, model.AvailabilityFromDate);

        // Converter ViewModel para User entity para preservar compatibilidade
        var user = new User
        {
            Id = model.UserId,
            FirstName = model.FirstName,
            Surname = model.Surname,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            Role = model.Role,
            Status = effectiveStatus
        };

        // Atualizar dados básicos (nome, email, telefone, role)
        _userRepository.UpdateUserAsManager(user);

        // Se status é OnLeave ou Sick, e há datas, criar período
        if ((model.Status == UserStatus.OnLeave || model.Status == UserStatus.Sick) &&
            model.AvailabilityFromDate.HasValue && model.AvailabilityToDate.HasValue)
        {
            // Se já existe período, remover primeiro
            if (model.AvailabilityPeriodId > 0)
            {
                _availabilityService.RemoveUnavailability(model.AvailabilityPeriodId);
            }

            // Criar novo período
            _availabilityService.MarkUnavailable(
                tenantId: 1,
                driverId: model.UserId,
                status: model.Status,
                fromDate: model.AvailabilityFromDate.Value,
                toDate: model.AvailabilityToDate.Value,
                reason: model.AvailabilityReason);

            if (effectiveStatus == model.Status)
            {
                TempData["SuccessMessage"] = string.Format(
                    "Staff member updated. {0} marked as {1} until {2:dd MMM yyyy}.",
                    model.FirstName,
                    model.Status == UserStatus.OnLeave ? "On Leave" : "Sick",
                    model.AvailabilityToDate.Value);
            }
            else
            {
                TempData["SuccessMessage"] = string.Format(
                    "Staff member updated. {0}'s {1} period has been scheduled from {2:dd MMM yyyy} to {3:dd MMM yyyy}.",
                    model.FirstName,
                    model.Status == UserStatus.OnLeave ? "On Leave" : "Sick",
                    model.AvailabilityFromDate.Value,
                    model.AvailabilityToDate.Value);
            }
        }
        else if (model.Status == UserStatus.Active && model.AvailabilityPeriodId > 0)
        {
            // Se voltou para Active e tinha período, remover
            _availabilityService.RemoveUnavailability(model.AvailabilityPeriodId);
            TempData["SuccessMessage"] = string.Format(
                "Staff member {0} reactivated. Now available for all operations.",
                model.FirstName);
        }
        else
        {
            TempData["SuccessMessage"] = "Staff member updated successfully.";
        }

        return RedirectToAction("Manage", new { id = model.UserId });
    }
    catch (ArgumentException ex)
    {
        TempData["ErrorMessage"] = string.Format("Cannot update: {0}", ex.Message);
        return RedirectToAction("Manage", new { id = model.UserId });
    }
    catch (Exception ex)
    {
        TempData["ErrorMessage"] = "An error occurred while updating staff member.";
        return RedirectToAction("Manage", new { id = model.UserId });
    }
}


    /// <summary>
    /// Carrega a tela de troca de senha do próprio usuário autenticado.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Authorize]
    public IActionResult ChangeOwnPassword()
    {
        return View();
    }


    /// <summary>
    /// Action para o próprio usuário trocar sua senha.
    /// </summary>
    /// <param name="newPassword"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeOwnPassword(string newPassword, string confirmPassword)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }


        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ModelState.AddModelError("newPassword", "Password cannot be empty.");
            return View();
        }

        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("confirmPassword", "Passwords do not match.");
            return View();
        }
        
        int userId = int.Parse(userIdClaim);
        string hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _userRepository.UpdatePassword(userId, hash);

        TempData["SuccessMessage"] = "Your password has been changed.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Edita as datas de um período de indisponibilidade via AJAX.
    /// Retorna JSON de sucesso ou erro para o modal na tela Manage.
    /// </summary>
    /// <param name="periodId">ID do período a editar.</param>
    /// <param name="driverId">ID do motorista dono do período.</param>
    /// <param name="fromDate">Nova data de início.</param>
    /// <param name="toDate">Nova data de retorno.</param>
    /// <param name="reason">Novo motivo (opcional).</param>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult EditLeave(int periodId, int driverId, DateTime fromDate, DateTime toDate, string? reason)
    {
        try
        {
            _availabilityService.UpdateLeave(periodId, driverId, tenantId: 1, fromDate, toDate, reason);
            return Json(new { success = true, message = "Availability period updated." });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while updating the period." });
        }
    }

    /// <summary>
    /// Cancela um período de indisponibilidade via AJAX.
    /// Retorna JSON de sucesso ou erro para o modal na tela Manage.
    /// </summary>
    /// <param name="periodId">ID do período a cancelar.</param>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [ValidateAntiForgeryToken]
    public IActionResult CancelLeave(int periodId)
    {
        try
        {
            _availabilityService.CancelLeave(periodId);
            return Json(new { success = true, message = "Availability period canceled." });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while canceling the period." });
        }
    }
    
}