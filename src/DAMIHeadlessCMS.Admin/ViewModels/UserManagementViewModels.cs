using System.ComponentModel.DataAnnotations;

namespace DAMIHeadlessCMS.Admin.ViewModels;

public sealed class UserListItemViewModel
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public List<string> Roles { get; init; } = new();
    public bool IsLockedOut { get; init; }
}

public sealed class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "L'email è obbligatoria.")]
    [EmailAddress(ErrorMessage = "Formato email non valido.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Nome visualizzato")]
    public string? DisplayName { get; set; }

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public List<string> SelectedRoles { get; set; } = new();

    public string[] AvailableRoles { get; set; } = Array.Empty<string>();

    public bool IsEdit => Id.HasValue;
}