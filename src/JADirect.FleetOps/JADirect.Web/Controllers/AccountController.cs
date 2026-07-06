using System.Security.Claims;
using JADirect.Application.Services;
using JADirect.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JADirect.Web.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _authService;

    public AccountController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        bool userWasBlockedByRateLimit = Request.Query.ContainsKey("blocked");
        
        if (userWasBlockedByRateLimit)
        {
            ViewBag.Error = "Too many login attempts. Please wait 1 minute and try again.";
        }
        return View();
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login-policy")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = _authService.ValidateUser(email, password);

        if (user == null)
        {
            ViewBag.Error = "Invalid email or password.";
            return View();
        }
        
        // Criando a identificação (Claims)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FirstName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("UserId", user.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        if (user.Role == UserRoles.Manager)
        {
            return RedirectToAction("Index", "Manager");
        }

        return RedirectToAction("SelectVehicle", "Driver");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}