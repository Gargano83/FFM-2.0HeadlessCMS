using System.ComponentModel.DataAnnotations;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

public class LoginViewModel
{
    [Required(ErrorMessage = "Inserisci l'email.")]
    [EmailAddress(ErrorMessage = "Email non valida.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Inserisci la password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ricordami")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
