namespace DAMIHeadlessCMS.Admin.Ffm.Data;

/// <summary>
/// Risolve l'Id utente legacy (dbo.WN_UTENTI.UT_ID) a partire dall'email
/// dell'utente CmsUser loggato nel backoffice, per popolare la colonna
/// FFM.SquadreRelGiocatori.IdUtente nelle scritture (aggiunta/rimozione/
/// aggiornamento giocatore in rosa). Corrispondenza 1:1 via email,
/// confermata dall'utente del progetto.
/// </summary>
public interface IFfmUserResolver
{
    /// <summary>
    /// Ritorna UT_ID per l'email indicata, oppure null se non c'è nessuna
    /// corrispondenza in WN_UTENTI (l'operazione chiamante decide come
    /// comportarsi: nessun fallback silenzioso su un valore "neutro" qui).
    /// </summary>
    Task<int?> ResolveIdUtenteAsync(string? email, CancellationToken ct = default);
}
