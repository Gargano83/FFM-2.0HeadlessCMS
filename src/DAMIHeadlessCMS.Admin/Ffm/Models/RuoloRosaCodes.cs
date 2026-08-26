namespace DAMIHeadlessCMS.Admin.Ffm.Models;

/// <summary>
/// Sigle di ruolo specifico assegnabili a un giocatore in rosa
/// (FFM.SquadreRelGiocatori.Ruolo) — più granulari del ruolo base di
/// FFM.Giocatori (Portiere/Difensore/Centrocampista/Attaccante): un
/// giocatore può averne uno o più contemporaneamente (es. difensore
/// centrale e braccetto). Persistite come lista delimitata da virgole nel
/// formato ",Cod1,Cod2,", compatibile col pattern LIKE '%,Cod,%' già in uso
/// altrove nello schema legacy.
/// </summary>
public static class RuoloRosaCodes
{
    /// <summary>
    /// Le 12 sigle valide, nell'ordine di formazione (portiere, linea
    /// difensiva, centrocampo, trequarti, attacco) — lo stesso ordine usato
    /// per ordinare la rosa in <see cref="FfmSquadraRepository"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> Tutti =
    [
        "P", "Ds", "Dc", "Dd", "B", "E", "M", "C", "W", "T", "A", "Pc"
    ];

    private static readonly HashSet<string> ValidiSet = new(Tutti, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Scompone il pattern ",Cod1,Cod2," salvato a database in una lista
    /// pulita, scartando in modo silenzioso eventuali codici non (più)
    /// validi — difesa in profondità, il client dovrebbe già inviare solo
    /// sigle valide tramite il selettore a tag.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => ValidiSet.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Ricompone la lista nel pattern ",Cod1,Cod2," per il salvataggio;
    /// null se la lista risulta vuota (colonna nullable — nessun ruolo
    /// specifico assegnato).
    /// </summary>
    public static string? Format(IEnumerable<string>? codici)
    {
        var validi = (codici ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c) && ValidiSet.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return validi.Count == 0 ? null : "," + string.Join(",", validi) + ",";
    }

    /// <summary>
    /// Ruolo specifico di default quando FFM.SquadreRelGiocatori.Ruolo non è
    /// ancora stato valorizzato per un giocatore, dedotto dal ruolo base di
    /// FFM.Giocatori secondo la convenzione concordata (Portiere→P,
    /// Difensore→Dc, Centrocampista→C, Attaccante→A). Usato solo per la
    /// visualizzazione/pre-selezione iniziale nel client: non scrive nulla a
    /// database finché l'utente non salva esplicitamente dal modal.
    /// </summary>
    public static string? MappaDaRuoloBase(string? ruoloBase) => ruoloBase switch
    {
        "Portiere" => "P",
        "Difensore" => "Dc",
        "Centrocampista" => "C",
        "Attaccante" => "A",
        _ => null
    };
}
