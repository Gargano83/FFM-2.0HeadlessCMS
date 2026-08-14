using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Vista di una squadra nell'Area Riservata (propria o "altra squadra").
/// Riusa direttamente i DTO del modulo FFM (già costruiti in fase 7 per il
/// backoffice) invece di duplicarli — stessi dati, stessa fonte.
/// </summary>
public class SquadraViewModel
{
    public required InfoSquadraDto Info { get; init; }

    public required IReadOnlyList<GiocatoreSquadraDto> Rosa { get; init; }

    /// <summary>
    /// True se l'utente corrente può modificare lo Stato dei giocatori di
    /// QUESTA squadra: o perché è la propria (e Info.AbilitaModifica lo
    /// consente), o perché l'utente è uno dei super-admin configurati in
    /// PublicSite:AreaRiservataSuperAdminUserIds (comunque soggetto a
    /// Info.AbilitaModifica — un super-admin non forza l'edit a mercato
    /// chiuso, si limita a poter editare squadre non proprie). NON governa
    /// l'aggiunta/rimozione di giocatori: vedi <see cref="PuoAggiungereRimuovere"/>.
    /// </summary>
    public bool PuoModificare { get; init; }

    /// <summary>
    /// True se l'utente corrente può aggiungere/rimuovere giocatori dalla rosa
    /// di questa squadra — riservato ai soli super-admin (con Info.AbilitaModifica),
    /// anche sulla propria squadra: un proprietario può modificare lo Stato dei
    /// propri giocatori (<see cref="PuoModificare"/>) ma non alterare la
    /// composizione della rosa. Governa la visibilità della barra di ricerca e
    /// del pulsante di rimozione.
    /// </summary>
    public bool PuoAggiungereRimuovere { get; init; }

    /// <summary>
    /// True quando PuoModificare è true ma la squadra NON è quella dell'utente
    /// corrente: sta modificando come super-admin. Usato solo per mostrare un
    /// avviso esplicito in pagina — non è una condizione di sicurezza (quella è
    /// già applicata interamente lato server per ogni singola scrittura).
    /// </summary>
    public bool IsSuperAdminOverride { get; init; }

    /// <summary>Elenco di tutte le squadre, per il selettore "vuoi consultare un'altra squadra?".</summary>
    public required IReadOnlyList<SquadraListItemDto> TutteLeSquadre { get; init; }
}
