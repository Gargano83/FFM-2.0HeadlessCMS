namespace DAMIHeadlessCMS.Admin.Ffm.Models;

/// <summary>
/// Pannello informativo/finanziario di una squadra (FFM.Squadre + aggregati
/// da FFM.SquadreRelGiocatori/FFM.Giocatori per la stagione attiva).
/// Corrisponde a InfoSquadra/Allenatore/Finanze del sistema legacy, appiattiti
/// in un unico DTO per semplicità lato frontend.
/// </summary>
public sealed class InfoSquadraDto
{
    public int IdSquadra { get; set; }
    public string NomeSquadra { get; set; } = string.Empty;
    public string? Presidente { get; set; }
    public string? VicePresidente { get; set; }
    public string? Allenatore { get; set; }
    public int DurataContrattoAllenatore { get; set; }
    public decimal StipendioAllenatore { get; set; }
    public int Tesserati { get; set; }
    public int InPrestito { get; set; }
    public int InRosa { get; set; }
    public int APrestito { get; set; }
    public int ListaA { get; set; }
    public int Under22InRosa { get; set; }
    public decimal RimanenzaStagionePrecedente { get; set; }
    public decimal RefillRanking { get; set; }
    public decimal RefillValoreSocieta { get; set; }
    public decimal RefillStadio { get; set; }
    public decimal RefillStipendi { get; set; }
    public decimal MonteStipendiAndata { get; set; }
    public decimal MonteStipendiRitorno { get; set; }
    public decimal BilancioMercato { get; set; }
    public decimal FairPlayFinanziario { get; set; }

    /// <summary>
    /// Flag stagionale (letto da FFM.Squadre) che indica se le azioni di modifica
    /// sulla rosa sono attualmente consentite (es. mercato aperto/chiuso). Il
    /// backoffice CMS lo espone così com'è al frontend, senza gestirne la logica.
    /// </summary>
    public bool AbilitaModifica { get; set; }
}

/// <summary>Riga elenco squadre (per la pagina indice /dami/ffm/squadre).</summary>
public sealed class SquadraListItemDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Presidente { get; set; }
    public string? Allenatore { get; set; }
}

/// <summary>Un giocatore nella rosa di una squadra (join FFM.Giocatori + FFM.SquadreRelGiocatori).</summary>
public sealed class GiocatoreSquadraDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public DateTime? DataDiNascita { get; set; }
    public string? Ruolo { get; set; }
    public decimal? ValoreDiMercato { get; set; }
    public decimal? Stipendio { get; set; }
    public string? Stato { get; set; }
    public int Mesi { get; set; }
    public bool U22 { get; set; }

    public string NomeCompleto => string.IsNullOrWhiteSpace(Nome)
        ? Cognome
        : string.IsNullOrWhiteSpace(Cognome) ? Nome : $"{Nome} {Cognome}";

    public int Eta => DataDiNascita.HasValue ? DateTime.Now.Year - DataDiNascita.Value.Year : 0;
}

/// <summary>Un giocatore svincolato (non presente in nessuna FFM.SquadreRelGiocatori), per il selettore "aggiungi giocatore".</summary>
public sealed class GiocatoreSvincolatoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string? Ruolo { get; set; }
    public DateTime? DataDiNascita { get; set; }
    public decimal? ValoreDiMercato { get; set; }
    public decimal? Stipendio { get; set; }

    public string NomeCompleto => string.IsNullOrWhiteSpace(Nome)
        ? Cognome
        : string.IsNullOrWhiteSpace(Cognome) ? Nome : $"{Nome} {Cognome}";
}

/// <summary>Corpo della richiesta per aggiornare stato/mesi di un giocatore in rosa.</summary>
public sealed class AggiornaRosaRequestDto
{
    public int Mesi { get; set; }
    public string? Stato { get; set; }
}

/// <summary>Corpo della richiesta per aggiungere un giocatore svincolato alla rosa.</summary>
public sealed class AggiungiGiocatoreRequestDto
{
    public decimal? ValoreDiMercato { get; set; }
    public decimal? Stipendio { get; set; }
}
