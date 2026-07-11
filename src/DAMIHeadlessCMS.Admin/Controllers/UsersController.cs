using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DAMIHeadlessCMS.Admin.ViewModels;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Controllers;

/// <summary>
/// Gestione utenti del backoffice (Identity dedicato DAMIHeadlessCMS). Elenco e
/// dettaglio sono accessibili in sola lettura anche a CmsOperator; creare,
/// modificare o (dis)abilitare un account e assegnare i ruoli resta invece
/// riservato a CmsAdmin (vedi gli attributi [Authorize] espliciti sulle
/// singole azioni di scrittura).
/// </summary>
[Route("dami/users")]
[Authorize(Policy = CmsAuthConstants.UsersViewPolicy)]
public class UsersController : Controller
{
    private readonly UserManager<CmsUser> _userManager;

    public UsersController(UserManager<CmsUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);

        var items = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Roles = roles.ToList(),
                IsLockedOut = await _userManager.IsLockedOutAsync(user)
            });
        }

        return View(items);
    }

    [HttpGet("create")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public IActionResult Create()
        => View(new UserFormViewModel { AvailableRoles = CmsRoles.All });

    [HttpPost("create")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        model.AvailableRoles = CmsRoles.All;

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "La password è obbligatoria per un nuovo utente.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new CmsUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password!);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        if (model.SelectedRoles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, model.SelectedRoles);
        }

        TempData["StatusMessage"] = $"Utente '{user.Email}' creato.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return View(new UserFormViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            SelectedRoles = roles.ToList(),
            AvailableRoles = CmsRoles.All
        });
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserFormViewModel model)
    {
        model.AvailableRoles = CmsRoles.All;
        model.Id = id;

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        // Password opzionale in modifica: se vuota, la validazione [Required] non si applica qui.
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.DisplayName = model.DisplayName;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToAdd = model.SelectedRoles.Except(currentRoles).ToList();
        var rolesToRemove = currentRoles.Except(model.SelectedRoles).ToList();

        if (rolesToAdd.Count > 0) await _userManager.AddToRolesAsync(user, rolesToAdd);
        if (rolesToRemove.Count > 0) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        TempData["StatusMessage"] = $"Utente '{user.Email}' aggiornato.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/toggle-lock")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(currentUserId, id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Non puoi disabilitare il tuo stesso account.";
            return RedirectToAction(nameof(Index));
        }

        var isLockedOut = await _userManager.IsLockedOutAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, isLockedOut ? null : DateTimeOffset.MaxValue);

        TempData["StatusMessage"] = isLockedOut
            ? $"Utente '{user.Email}' riabilitato."
            : $"Utente '{user.Email}' disabilitato.";

        return RedirectToAction(nameof(Index));
    }
}