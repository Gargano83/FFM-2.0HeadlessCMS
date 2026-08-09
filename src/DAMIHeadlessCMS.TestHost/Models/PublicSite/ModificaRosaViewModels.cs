using System.ComponentModel.DataAnnotations;
using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

public class ModificaGiocatoreViewModel
{
    public int IdGiocatore { get; set; }

    public string NomeCompleto { get; set; } = string.Empty;

    public string? Ruolo { get; set; }

    [Range(0, 120, ErrorMessage = "Valore non valido.")]
    [Display(Name = "Mesi")]
    public int Mesi { get; set; }

    [Display(Name = "Stato")]
    public string? Stato { get; set; }
}

public class AggiungiGiocatoreViewModel
{
    public IReadOnlyList<GiocatoreSvincolatoDto> Disponibili { get; set; } = [];

    [Required(ErrorMessage = "Seleziona un giocatore.")]
    [Display(Name = "Giocatore")]
    public int? IdGiocatoreSelezionato { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valore non valido.")]
    [Display(Name = "Valore di mercato")]
    public decimal? ValoreDiMercato { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valore non valido.")]
    [Display(Name = "Stipendio")]
    public decimal? Stipendio { get; set; }
}
