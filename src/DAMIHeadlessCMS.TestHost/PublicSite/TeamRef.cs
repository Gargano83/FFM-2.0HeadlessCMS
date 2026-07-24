namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Riferimento leggero a una squadra (nome + logo), risolto una volta e riusato in tutti
/// i widget della pagina Statistiche (Vincitore, Finalista, Sede finale, Primo/Secondo/Terzo,
/// colonne del pivot Allenatori/Presidenti, ecc.) — evita di ripetere la stessa risoluzione
/// FFM.Squadre in ogni singolo blocco.
/// </summary>
public sealed record TeamRef(string Name, string? LogoPath);
