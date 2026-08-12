using System.Text.Json;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Parsing di basso livello di <see cref="Core.Entities.CmsPage.ContentJson"/> (elenco di
/// blocchi <c>{ type, ... }</c>), condiviso tra <see cref="Controllers.PagesController"/>
/// (rendering completo di una CmsPage nativa) e qualunque controller ad-hoc che voglia
/// riusare solo alcuni blocchi di una CmsPage esistente per arricchire un proprio
/// template — es. <see cref="Controllers.StatisticheController"/>, che compone un blocco
/// "html" curato da backoffice sopra il proprio accordion codificato, senza passare dal
/// routing generico di PagesController.Show (che qui non entra mai in gioco: /statistiche
/// è già intercettata dal controller dedicato — vedi Program.cs). Un ContentJson non
/// valido/vuoto restituisce sempre una lista vuota, mai un'eccezione: una pagina "rotta"
/// non deve mai far cadere l'intero sito.
/// </summary>
public static class CmsPageContentParser
{
    public static IReadOnlyList<JsonElement> ParseBlocks(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            // Clone: il JsonElement normalmente perde validità quando il JsonDocument
            // sorgente viene smaltito (fine del blocco using) — Clone lo rende autonomo.
            return document.RootElement.EnumerateArray().Select(b => b.Clone()).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Concatena l'HTML di tutti i blocchi "html" (nell'ordine in cui compaiono),
    /// ignorando altri tipi di blocco (entityList/component) — pensato per un
    /// controller ad-hoc che vuole solo il testo/immagini curati da backoffice,
    /// non l'intera composizione di una CmsPage.
    /// </summary>
    public static string? GetHtmlBlocksConcatenated(string? contentJson)
    {
        var htmlParts = ParseBlocks(contentJson)
            .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "html")
            .Select(b => b.TryGetProperty("html", out var h) ? h.GetString() : null)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        return htmlParts.Count == 0 ? null : string.Join(Environment.NewLine, htmlParts);
    }
}
