using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DAMIHeadlessCMS.Admin.ViewModels;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Controllers;

[Route("dami/account")]
public class AccountController : Controller
{
    private readonly SignInManager<CmsUser> _signInManager;

    public AccountController(SignInManager<CmsUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/dami");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(model.ReturnUrl ?? "/dami");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Account temporaneamente bloccato per troppi tentativi falliti. Riprova tra qualche minuto."
            : "Email o password non corretti.");

        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("denied")]
    [AllowAnonymous]
    public IActionResult Denied() => View();
}