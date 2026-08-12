using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Modello per il rendering pubblico di una <see cref="Core.Entities.CmsPage"/>: i blocchi
/// grezzi di ContentJson vengono risolti lato controller (in particolare "entityList", che
/// richiede una query al database) prima di arrivare alla view, così la view resta solo
/// presentazionale. Vedi <see cref="Controllers.PagesController.Show"/>.
/// </summary>
public sealed class CmsPageViewModel
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<CmsPageBlockViewModel> Blocks { get; init; } = [];
}

/// <summary>Base per i blocchi risolti di una pagina. Vedi le implementazioni per i tipi supportati.</summary>
public abstract record CmsPageBlockViewModel;

/// <summary>Blocco di testo/HTML libero, scritto con l'editor rich text del backoffice.</summary>
public sealed record CmsHtmlBlockViewModel(string Html) : CmsPageBlockViewModel;

/// <summary>
/// Blocco "Lista entità": righe di una tabella già scaffoldata, lette con le FK già
/// risolte in etichetta (vedi <see cref="PublicSite.LegacyContentReader.GetRowsForDisplayAsync"/>).
/// Le colonne mostrate sono quelle marcate "in elenco" nello scaffolding — stesso criterio
/// già usato nella griglia dati del backoffice, nessun concetto nuovo da configurare.
/// </summary>
public sealed record CmsEntityListBlockViewModel(
    string? Title,
    string EntityDisplayName,
    IReadOnlyList<CmsEntityListColumnViewModel> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows) : CmsPageBlockViewModel;

public sealed record CmsEntityListColumnViewModel(string ColumnName, string DisplayName, EditorType EditorType);
