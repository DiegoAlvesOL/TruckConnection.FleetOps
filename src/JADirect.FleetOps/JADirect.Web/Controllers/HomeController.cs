// Purpose   : Ponto de entrada da aplicação. Redireciona o usuário para o
//             dashboard correto com base no seu papel. Mantém a Error action
//             exigida pelo pipeline de tratamento de erros do ASP.NET Core.
// Consumed by: Pipeline de roteamento do ASP.NET Core e middleware de erros.
// Layer     : Presentation — Web

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using JADirect.Web.Models;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controller de entrada da aplicação.
/// Redireciona para o dashboard correto baseado no papel do usuário autenticado.
/// Mantém a action Error exigida pelo pipeline de tratamento de erros do ASP.NET Core.
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// Redireciona o usuário para o dashboard correspondente ao seu papel.
    /// Managers vão para Manager/Index. Drivers vão para Driver/SelectVehicle.
    /// Usuários não autenticados vão para o Login.
    /// </summary>
    public IActionResult Index()
    {
        if (User.IsInRole("Manager"))
        {
            return RedirectToAction("Index", "Manager");
        }

        if (User.IsInRole("Driver"))
        {
            return RedirectToAction("SelectVehicle", "Driver");
        }

        return RedirectToAction("Login", "Account");
    }

    /// <summary>
    /// Exibe a página de erro genérica da aplicação.
    /// Chamada automaticamente pelo middleware de tratamento de erros configurado no Program.cs.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}