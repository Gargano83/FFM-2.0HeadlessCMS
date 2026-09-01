namespace DAMIHeadlessCMS.Admin.Ffm.Models;

/// <summary>
/// Una riga del catalogo estendibile <c>FFM.DivisaTemplate</c> — la fonte di
/// verità per quali template di maglia esistono. Né la galleria di selezione
/// né il motore di rendering ricevono mai un elenco/numero di template
/// hardcoded: leggono sempre da qui. Aggiungere un template = un nuovo record
/// + i file corrispondenti sotto <c>wwwroot/img/divisa/template/{CartellaAsset}/</c>
/// (<c>icona.png</c>, <c>base.png</c> zona Colore1, <c>maniche.png</c> zona
/// Colore2) — nessuna modifica al codice.
/// </summary>
public sealed class DivisaTemplateDto
{
    public int Id { get; set; }

    /// <summary>Etichetta descrittiva, uso interno/futura UI di gestione — non mostrata necessariamente all'utente finale.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome cartella sotto <c>wwwroot/img/divisa/template/</c> che contiene <c>icona.png</c>/<c>base.png</c>/<c>maniche.png</c> per questo template.</summary>
    public string CartellaAsset { get; set; } = string.Empty;

    public int Ordine { get; set; }

    /// <summary>
    /// Se false il template è stato ritirato dalla galleria di selezione
    /// (non compare più tra le scelte disponibili) ma resta valido per le
    /// squadre che lo hanno già selezionato in passato — non va mai eliminato
    /// fisicamente finché è referenziato da <c>FFM.SquadreMaglia</c>.
    /// </summary>
    public bool Attivo { get; set; }
}

/// <summary>
/// Personalizzazione divisa (solo maglia, per ora) di una squadra — lettura
/// combinata di <c>FFM.SquadreMaglia</c> e del template collegato. Restituita
/// sempre valorizzata da <see cref="Data.IFfmDivisaRepository.GetDivisaAsync"/>
/// anche per una squadra che non ha ancora personalizzato nulla (vedi
/// <see cref="NonAncoraPersonalizzata"/>): la UI ha sempre qualcosa da mostrare,
/// mai uno stato "vuoto" da gestire a parte.
/// </summary>
public sealed class DivisaSquadraDto
{
    public int IdSquadra { get; set; }

    public int IdTemplate { get; set; }

    /// <summary>Cartella asset del template selezionato — comoda qui per non costringere il chiamante a una seconda lookup su <see cref="Data.IFfmDivisaTemplateRepository"/> solo per renderizzare l'anteprima corrente.</summary>
    public string CartellaAssetTemplate { get; set; } = string.Empty;

    /// <summary>Zona 1 (intera sagoma, <c>base.png</c>). Formato esadecimale <c>#RRGGBB</c>, come restituito da un <c>&lt;input type="color"&gt;</c>.</summary>
    public string Colore1 { get; set; } = "#FFFFFF";

    /// <summary>Zona 2 (maniche, <c>maniche.png</c>).</summary>
    public string Colore2 { get; set; } = "#000000";

    /// <summary>Zona 3 (colletto/polsini, asset condiviso <c>condivisi/colletto.png</c>).</summary>
    public string Colore3 { get; set; } = "#000000";

    /// <summary>Testo libero dello sponsor (fuori scope: patch/badge di competizione). Null/vuoto = nessuno sponsor mostrato.</summary>
    public string? TestoSponsor { get; set; }

    public string? ColoreTestoSponsor { get; set; }

    /// <summary>Facoltativo — contorno del testo sponsor, per leggibilità su maglie chiare.</summary>
    public string? ColoreContornoTestoSponsor { get; set; }

    /// <summary>Facoltativo — sfondo/pillola dietro il testo sponsor.</summary>
    public string? ColoreSfondoTestoSponsor { get; set; }

    /// <summary>
    /// URL del PNG "cotto" una volta dal motore di rendering (composizione di
    /// tutti i layer + sponsor) e riusato staticamente come <c>&lt;img&gt;</c>
    /// ovunque la maglia va mostrata — mai ri-renderizzato live fuori dal
    /// configuratore stesso. Null finché non è mai stato generato.
    /// </summary>
    public string? UrlImmagineGenerata { get; set; }

    public DateTime? DataUltimaModifica { get; set; }

    /// <summary>
    /// True se la squadra non ha ancora nessuna riga in
    /// <c>FFM.SquadreMaglia</c>: gli altri campi sono un default sensato
    /// (primo template attivo per <see cref="DivisaTemplateDto.Ordine"/>,
    /// colori neutri), non una scelta già salvata.
    /// </summary>
    public bool NonAncoraPersonalizzata { get; set; }
}

/// <summary>Corpo della richiesta per creare/aggiornare la personalizzazione divisa di una squadra (upsert su <c>FFM.SquadreMaglia</c>).</summary>
public sealed class AggiornaDivisaRequestDto
{
    public int IdTemplate { get; set; }

    public string Colore1 { get; set; } = string.Empty;

    public string Colore2 { get; set; } = string.Empty;

    public string Colore3 { get; set; } = string.Empty;

    public string? TestoSponsor { get; set; }

    public string? ColoreTestoSponsor { get; set; }

    public string? ColoreContornoTestoSponsor { get; set; }

    public string? ColoreSfondoTestoSponsor { get; set; }

    /// <summary>
    /// Valorizzato dal chiamante dopo che il motore di rendering ha "cotto" e
    /// salvato il PNG finale — questo repository si limita a persisterne
    /// l'URL, non genera né salva l'immagine.
    /// </summary>
    public string? UrlImmagineGenerata { get; set; }
}

/// <summary>
/// Risposta dell'endpoint <c>GET .../divisa</c> (fase 5): catalogo dei template
/// selezionabili + personalizzazione corrente della squadra, nello stesso
/// oggetto — evita al chiamante due round-trip separati per popolare la
/// galleria e precompilare i controlli con i valori già salvati.
/// </summary>
public sealed class DivisaConfiguratoreDto
{
    public required IReadOnlyList<DivisaTemplateDto> CatalogoTemplate { get; init; }

    public required DivisaSquadraDto Divisa { get; init; }

    /// <summary>
    /// True se l'utente corrente può salvare modifiche per questa squadra —
    /// stessa regola (<c>AreaRiservataAuthorizationService.CanEdit</c>) già
    /// usata per la rosa e i dati anagrafici della squadra. Solo informativo
    /// per la UI (mostra/nasconde i controlli di modifica): l'endpoint PUT
    /// ri-verifica sempre il permesso lato server, non si fida mai di questo
    /// valore.
    /// </summary>
    public bool PuoModificare { get; init; }
}

/// <summary>
/// Corpo della richiesta <c>PUT .../divisa</c> (fase 5). Rispetto a
/// <see cref="AggiornaDivisaRequestDto"/> (il contratto scritto a mano del
/// repository) sostituisce <c>UrlImmagineGenerata</c> con il PNG "cotto" dal
/// motore di rendering lato client, come data URL (es. da
/// <c>canvas.toDataURL("image/png")</c>): salvare il file fisico e calcolarne
/// l'URL finale è responsabilità dell'host (o di
/// <c>DAMIHeadlessCMS.TestHost</c>, in fase di validazione) tramite
/// <c>IFileStorageProvider</c>, non del client né del repository.
/// </summary>
public sealed class AggiornaDivisaApiRequestDto
{
    public int IdTemplate { get; set; }

    public string Colore1 { get; set; } = string.Empty;

    public string Colore2 { get; set; } = string.Empty;

    public string Colore3 { get; set; } = string.Empty;

    public string? TestoSponsor { get; set; }

    public string? ColoreTestoSponsor { get; set; }

    public string? ColoreContornoTestoSponsor { get; set; }

    public string? ColoreSfondoTestoSponsor { get; set; }

    /// <summary>
    /// Data URL PNG generato lato client al momento del salvataggio (es.
    /// <c>"data:image/png;base64,..."</c>). Null/vuoto = non rigenerare
    /// l'immagine: viene mantenuto <c>UrlImmagineGenerata</c> già salvato in
    /// precedenza, così un client che aggiorna solo i dati (senza aver ancora
    /// ricomposto il PNG) non perde l'ultima immagine valida.
    /// </summary>
    public string? ImmagineGenerataBase64 { get; set; }
}
