namespace DAMIHeadlessCMS.Core.Entities;

/// <summary>
/// Riga di log di audit: chi ha creato/modificato/eliminato un'entità
/// CMS-native (Pagine, Menu, voci di Menu, Utenti) e quando. Generata
/// automaticamente da <c>CmsDbContext.SaveChangesAsync</c> leggendo il
/// ChangeTracker di EF Core — nessuna scrittura esplicita richiesta nei
/// controller, quindi nessun rischio di dimenticarsene in un punto nuovo.
///
/// Copre solo le entità EF-native del CMS. NON copre le tabelle applicative
/// scaffoldate (sezione "Dati"): quelle sono lette/scritte con SQL dinamico
/// via ADO.NET (<c>IGenericEntityRepository</c>), fuori dal ChangeTracker di
/// EF Core — un audit su quelle richiederebbe un meccanismo separato,
/// deliberatamente fuori scope in questa fase.
///
/// Nota sul menu: <c>MenusController.Save</c> usa una strategia
/// "full-replace" (elimina tutte le voci e reinserisce), quindi un singolo
/// salvataggio dell'albero genera più righe (una per voce eliminata/creata),
/// non un singolo "Update" — riflette accuratamente come funziona il
/// salvataggio, non è un difetto del log.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    /// <summary>Id dell'utente che ha eseguito l'operazione, se autenticato al momento del salvataggio.</summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Email dell'utente salvata "a valore" (non come FK): resta leggibile
    /// anche se l'utente viene poi eliminato dal sistema.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>Nome semplice del tipo di entità coinvolta (es. "CmsPage", "CmsUser").</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>Id dell'entità coinvolta, come stringa (i tipi di chiave differiscono per entità).</summary>
    public string EntityId { get; set; } = null!;

    /// <summary>"Create" | "Update" | "Delete".</summary>
    public string Action { get; set; } = null!;

    /// <summary>Descrizione leggibile per la UI, es. "Pagina "Chi siamo" (chi-siamo)".</summary>
    public string? Summary { get; set; }
}
