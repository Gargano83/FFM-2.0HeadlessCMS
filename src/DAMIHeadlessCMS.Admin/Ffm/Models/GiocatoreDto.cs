using System.ComponentModel.DataAnnotations;

namespace DAMIHeadlessCMS.Admin.Ffm.Models;

/// <summary>
/// Rappresenta una riga di FFM.Giocatori. Sostituisce il DTO legacy
/// (GiocatoriDTO) che portava ValoreDiMercato/Stipendio come stringhe
/// formattate "all'italiana": qui i valori numerici sono tipizzati
/// correttamente, e la formattazione/parsing resta responsabilità della UI
/// (Grid Syncfusion) al momento della visualizzazione.
/// </summary>
public sealed class GiocatoreDto
{
    /// <summary>0 o negativo per un giocatore non ancora salvato (nuova riga in griglia).</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Il nome è obbligatorio.")]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Il cognome è obbligatorio.")]
    [MaxLength(150)]
    public string Cognome { get; set; } = string.Empty;

    public DateTime? DataDiNascita { get; set; }

    [MaxLength(50)]
    public string? Ruolo { get; set; }

    public decimal? ValoreDiMercato { get; set; }

    public decimal? Stipendio { get; set; }

    public DateTime? DataAggiornamento { get; set; }

    public string? Note { get; set; }
}
