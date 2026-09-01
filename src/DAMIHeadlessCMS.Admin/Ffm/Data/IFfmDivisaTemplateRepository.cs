using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

/// <summary>
/// Accesso in sola lettura al catalogo <c>FFM.DivisaTemplate</c>. Scritto a
/// mano (non metadata-driven) come <see cref="IFfmSquadraRepository"/>: la
/// gestione del catalogo (aggiungere/ritirare un template) in v1 passa da un
/// insert/update SQL diretto, non da una UI di backoffice — vedi piano
/// "Personalizzazione divisa squadra", sezione "Estensibilità del catalogo
/// template".
/// </summary>
public interface IFfmDivisaTemplateRepository
{
    /// <summary>Catalogo dei template attivi, ordinato per <see cref="DivisaTemplateDto.Ordine"/> — per la galleria di selezione (FASE 1 del configuratore).</summary>
    Task<IReadOnlyList<DivisaTemplateDto>> GetTemplateAttiviAsync(CancellationToken ct = default);

    /// <summary>
    /// Un singolo template per id, incluso se non più <see cref="DivisaTemplateDto.Attivo"/> —
    /// serve al motore di rendering e alla lettura della personalizzazione di
    /// una squadra che ha selezionato un template nel frattempo ritirato dalla
    /// galleria: deve continuare a vederlo, solo non a sceglierlo di nuovo.
    /// </summary>
    Task<DivisaTemplateDto?> GetTemplateByIdAsync(int idTemplate, CancellationToken ct = default);
}
