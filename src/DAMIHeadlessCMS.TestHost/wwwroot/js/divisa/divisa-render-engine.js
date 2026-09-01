// Motore di rendering Canvas 2D per la personalizzazione divisa squadra
// (vedi piano-divisa-squadra.md, sezione "4. Motore di rendering").
//
// Modulo vanilla JS, senza dipendenze (niente jQuery/Fabric.js/Spectrum, a
// differenza del vecchio configuratore FFM3.1): riceve un <canvas> e un
// oggetto opzioni, disegna la maglia composta da 4 layer:
//   1. base.png       (tinto Colore1 — corpo/sagoma intera)
//   2. maniche.png    (tinto Colore2 — disegnato sopra, altera solo l'area maniche)
//   3. colletto.png   (condiviso, tinto Colore3 — disegnato sopra, altera solo colletto/polsini)
//   4. ombre.png      (condiviso, applicato con blend "overlay" — luci/ombre/pieghe tessuto)
// più, opzionalmente, il testo sponsor.
//
// Non è legato a nessun host specifico: riceve gli URL base come parametro,
// così lo stesso file può essere riusato identico sia in DAMIHeadlessCMS.TestHost
// (validazione, fase 4) sia in FFM2.0Core (integrazione finale, fase 5/6) —
// cambia solo dove viene richiamato e con quali URL, non il motore stesso.
window.DivisaRenderEngine = (function () {
    "use strict";

    const DIMENSIONE_CANVAS = 512;

    // Fase 8.3 — migliorie al lettering sponsor (vedi piano-divisa-squadra.md,
    // sezione 8). Fonte unica di verità per i 3 preset di posizione verticale:
    // "Alto" riproduce esattamente cy = 0.55, l'unica posizione fissa esistita
    // prima di questa fase — invariata per non alterare la resa delle
    // personalizzazioni già salvate. "Centro" e "Basso" sono le due nuove
    // posizioni, verso il fondo maglia. Esportato sotto (vedi return finale)
    // così un'eventuale UI di selezione (fase 8.4) legge le chiavi da qui,
    // nessun elenco duplicato altrove — stesso principio già adottato per
    // window.DivisaFonts.MANIFESTO.
    // Valori ricalibrati in fase 8.4 su richiesta dell'utente (screenshot del
    // configuratore reale): "Alto" è ora la posizione alta sul petto (sotto il
    // colletto), "Centro" è la posizione centrale usata storicamente da "Alto"
    // (0.55 — invariata per compatibilità visiva con le divise già personalizzate),
    // "Basso" è leggermente più in basso del centro.
    const POSIZIONI_TESTO_SPONSOR = {
        Alto: { cy: 0.37 },
        Centro: { cy: 0.55 },
        Basso: { cy: 0.60 }
    };

    // Famiglia di fallback quando il font brandizzato richiesto non può essere
    // usato (chiave "Predefinito", chiave sconosciuta, o window.DivisaFonts
    // non incluso nella pagina) — stessa stringa già in uso prima della fase 8.3.
    const FONT_PREDEFINITO = "system-ui, -apple-system, Segoe UI, Roboto, sans-serif";

    // Parametri fissi (non regolabili dall'utente, come da decisione presa in
    // fase di analisi) dell'ombreggiatura testo, espressi come frazione della
    // dimensione del font per restare proporzionati a qualunque dimensione/auto-fit.
    const OMBRA_SPONSOR_BLUR_FRAZIONE = 0.18;
    const OMBRA_SPONSOR_OFFSET_X_FRAZIONE = 0.05;
    const OMBRA_SPONSOR_OFFSET_Y_FRAZIONE = 0.10;

    // Raggio della curvatura ad arco, fisso e moderato come da decisione
    // dell'utente (non regolabile) — espresso come frazione della dimensione
    // del CANVAS, non della dimensione del font. È una scelta deliberata:
    // se il raggio scalasse con la dimensione del font (che l'auto-fit può
    // ridurre parecchio per uno sponsor lungo), l'ampiezza angolare
    // dell'arco (larghezzaTotale/raggio) esploderebbe proprio nei casi in
    // cui l'auto-fit interviene di più, producendo un arco assurdo che si
    // avvolge oltre il possibile invece di restare "fisso e moderato". Con
    // un raggio ancorato al canvas, l'ampiezza dell'arco varia solo in base
    // alla lunghezza effettiva del testo disegnato (più lungo = curva più
    // pronunciata, più corto = curva più leggera), ma il raggio della curva
    // resta sempre lo stesso indipendentemente da font/dimensione/auto-fit.
    const ARCO_SPONSOR_RAGGIO_FRAZIONE_CANVAS = 0.62;

    // Larghezza massima "stampabile" per il testo sponsor, come frazione della
    // larghezza canvas — usata sia dall'auto-fit (fase 8.3) sia, indirettamente,
    // per contenere l'ingombro orizzontale del lettering ad arco (la cui
    // lunghezza d'arco è costruita per coincidere con la larghezza piatta del
    // testo, vedi disegnaTestoAdArco).
    const LARGHEZZA_MASSIMA_SPONSOR_FRAZIONE = 0.74;

    /**
     * Carica un'immagine da URL come Promise. crossOrigin "anonymous" non è
     * strettamente necessario per asset serviti dallo stesso host, ma non fa
     * male e allinea il comportamento a un eventuale storage esterno futuro.
     */
    function caricaImmagine(url) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.crossOrigin = "anonymous";
            img.onload = () => resolve(img);
            img.onerror = () => reject(new Error("Impossibile caricare l'asset: " + url));
            img.src = url;
        });
    }

    /**
     * "Tinta piatta": sostituisce il colore RGB di "img" con "colore" pieno,
     * mantenendo intatto il canale alpha originale (quindi la sagoma/bordi
     * anti-aliasati restano identici). Il colore effettivo con cui è stato
     * disegnato l'asset sorgente (nei file migrati dal vecchio configuratore
     * FFM3.1 sono tinte piene rosso/blu/verde, usate dal designer originale
     * solo come riferimento visivo in fase di disegno) non ha alcun peso:
     * ogni file è trattato come uno stencil a sé, la tinta è decisa qui.
     *
     * Tecnica: disegna l'immagine, poi in composite "source-in" riempie
     * l'intero canvas con il colore — il risultato è visibile solo dove
     * l'immagine sorgente aveva già alpha > 0, con lo stesso valore di alpha
     * (bordi sfumati inclusi).
     */
    function tingiSagoma(img, colore) {
        const c = document.createElement("canvas");
        c.width = img.naturalWidth || DIMENSIONE_CANVAS;
        c.height = img.naturalHeight || DIMENSIONE_CANVAS;
        const ctx = c.getContext("2d");
        ctx.drawImage(img, 0, 0, c.width, c.height);
        ctx.globalCompositeOperation = "source-in";
        ctx.fillStyle = colore;
        ctx.fillRect(0, 0, c.width, c.height);
        ctx.globalCompositeOperation = "source-over";
        return c;
    }

    /**
     * Risolve la chiave font ("Predefinito"/"Anton"/"BebasNeue"/...) nella
     * stringa famiglia CSS pronta per ctx.font, delegando il caricamento vero
     * e proprio a window.DivisaFonts.caricaFont (fase 8.2, modulo separato
     * che vive nel wwwroot della RCL DAMIHeadlessCMS.Admin insieme ai font).
     * Non lancia mai eccezioni: se lo script divisa-fonts.js non è incluso
     * nella pagina (o la chiave non è riconosciuta), ricade sempre sul font
     * di sistema — il motore resta utilizzabile anche senza quello script,
     * semplicemente senza i font brandizzati.
     */
    async function risolviFamigliaFont(chiaveFont) {
        if (window.DivisaFonts && typeof window.DivisaFonts.caricaFont === "function") {
            try {
                return await window.DivisaFonts.caricaFont(chiaveFont);
            } catch (errore) {
                return FONT_PREDEFINITO;
            }
        }
        return FONT_PREDEFINITO;
    }

    /**
     * Calcola la dimensione del font in px quando l'auto-fit è attivo:
     * misura il testo alla dimensione base con la famiglia già risolta e, se
     * eccede la larghezza massima stampabile, lo scala proporzionalmente
     * (mai lo ingrandisce oltre la base — l'auto-fit è pensato per contenere
     * l'overflow, non per massimizzare la dimensione). Un margine di
     * sicurezza del 4% evita che il testo tocchi esattamente il bordo utile,
     * e una dimensione minima (40% della base) evita che uno sponsor
     * estremamente lungo collassi in un testo illeggibile.
     */
    function calcolaDimensioneAutoFit(ctx, testo, famigliaFont, dimensioneBase, larghezzaMassima) {
        ctx.font = "700 " + dimensioneBase + "px " + famigliaFont;
        const larghezzaBase = ctx.measureText(testo).width;
        if (larghezzaBase <= larghezzaMassima || larghezzaBase === 0) {
            return dimensioneBase;
        }
        const fattoreScala = (larghezzaMassima / larghezzaBase) * 0.96;
        const dimensioneMinima = Math.round(dimensioneBase * 0.4);
        return Math.max(dimensioneMinima, Math.round(dimensioneBase * fattoreScala));
    }

    /**
     * Lettering ad arco — funzione volutamente isolata e autosufficiente
     * (nessuna logica condivisa con il percorso "dritto" oltre a ctx.font,
     * già impostato dal chiamante, e all'ombreggiatura, già attiva su ctx
     * se richiesta): l'utente ha accettato questa funzionalità con la
     * riserva esplicita di rimuoverla integralmente se la resa finale non
     * fosse soddisfacente, quindi un'eventuale rimozione futura deve potersi
     * limitare a cancellare questa funzione + la chiamata che la invoca +
     * il campo DTO/colonna DB, senza toccare nient'altro nel motore.
     *
     * Tecnica: disegna un carattere alla volta lungo una circonferenza il
     * cui raggio è fisso e ancorato al canvas (ARCO_SPONSOR_RAGGIO_FRAZIONE_CANVAS,
     * non alla dimensione del font — vedi commento sulla costante per il
     * perché), con il centro della circonferenza posizionato sotto il punto
     * cy richiesto — il risultato è un arco che si inarca verso l'alto al
     * centro e scende leggermente verso le estremità (lo stesso effetto "a
     * sorriso" tipico del lettering sponsor curvo). L'ampiezza angolare
     * dell'arco dipende solo dalla larghezza piatta effettiva del testo
     * (misurata carattere per carattere alla dimensione già scelta, manuale
     * o auto-fit): un testo più lungo curva di più, uno corto curva meno,
     * ma il raggio della curva resta sempre lo stesso.
     *
     * Non disegna uno sfondo/pillola dietro il testo (a differenza del
     * percorso dritto): un rettangolo non seguirebbe la curva e il
     * risultato visivo sarebbe peggiore che ometterlo — vedi chiamata in
     * disegnaSponsor, che salta il rettangolo quando l'arco è attivo.
     */
    function disegnaTestoAdArco(ctx, testo, cx, cy, dimensioneFont, opzioni) {
        const raggio = DIMENSIONE_CANVAS * ARCO_SPONSOR_RAGGIO_FRAZIONE_CANVAS;
        const caratteri = Array.from(testo);
        const larghezze = caratteri.map((ch) => ctx.measureText(ch).width);
        const larghezzaTotale = larghezze.reduce((somma, l) => somma + l, 0);
        if (larghezzaTotale === 0) {
            return;
        }

        const angoloTotale = larghezzaTotale / raggio;
        const centroCerchioY = cy + raggio;
        const larghezzaLinea = opzioni.coloreContornoTestoSponsor ? Math.max(2, Math.round(dimensioneFont * 0.08)) : 0;

        let angoloCorrente = -angoloTotale / 2;
        caratteri.forEach((carattere, indice) => {
            const angoloCarattere = larghezze[indice] / raggio;
            const angoloCentro = angoloCorrente + angoloCarattere / 2;
            const x = cx + raggio * Math.sin(angoloCentro);
            const y = centroCerchioY - raggio * Math.cos(angoloCentro);

            ctx.save();
            ctx.translate(x, y);
            ctx.rotate(angoloCentro);

            if (opzioni.coloreContornoTestoSponsor) {
                ctx.lineWidth = larghezzaLinea;
                ctx.strokeStyle = opzioni.coloreContornoTestoSponsor;
                ctx.strokeText(carattere, 0, 0);
            }
            ctx.fillStyle = opzioni.coloreTestoSponsor || "#000000";
            ctx.fillText(carattere, 0, 0);

            ctx.restore();
            angoloCorrente += angoloCarattere;
        });
    }

    /**
     * Disegna il testo sponsor. Contorno, sfondo e ombra sono facoltativi
     * (null/undefined per disattivarli) — coerente con i rispettivi campi
     * nullable nel DTO. Dalla fase 8.3 supporta anche: preset di posizione
     * verticale (POSIZIONI_TESTO_SPONSOR), font brandizzato (tramite
     * risolviFamigliaFont/window.DivisaFonts), dimensione manuale o
     * auto-fit, e lettering ad arco (disegnaTestoAdArco, isolato).
     */
    async function disegnaSponsor(ctx, opzioni) {
        const testo = (opzioni.testoSponsor || "").trim();
        if (!testo) {
            return;
        }

        const posizione = POSIZIONI_TESTO_SPONSOR[opzioni.posizioneTestoSponsor] || POSIZIONI_TESTO_SPONSOR.Alto;
        const cx = DIMENSIONE_CANVAS / 2;
        const cy = DIMENSIONE_CANVAS * posizione.cy;
        const dimensioneBase = Math.round(DIMENSIONE_CANVAS * 0.066); // ~34px a 512, invariata rispetto a prima della fase 8.3
        const larghezzaMassima = DIMENSIONE_CANVAS * LARGHEZZA_MASSIMA_SPONSOR_FRAZIONE;

        const famigliaFont = await risolviFamigliaFont(opzioni.fontTestoSponsor);

        ctx.save();
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";

        const dimensioneFont = opzioni.autoFitTestoSponsor
            ? calcolaDimensioneAutoFit(ctx, testo, famigliaFont, dimensioneBase, larghezzaMassima)
            : Math.max(10, Math.round(dimensioneBase * ((opzioni.dimensioneTestoSponsor || 100) / 100)));

        ctx.font = "700 " + dimensioneFont + "px " + famigliaFont;

        // Sfondo/pillola dietro il testo: solo per il lettering dritto, non
        // per l'arco (vedi commento su disegnaTestoAdArco).
        if (opzioni.coloreSfondoTestoSponsor && !opzioni.letteringAdArcoTestoSponsor) {
            const metriche = ctx.measureText(testo);
            const paddingX = dimensioneFont * 0.4;
            const paddingY = dimensioneFont * 0.3;
            const larghezza = metriche.width + paddingX * 2;
            const altezza = dimensioneFont + paddingY * 2;
            ctx.fillStyle = opzioni.coloreSfondoTestoSponsor;
            ctx.fillRect(cx - larghezza / 2, cy - altezza / 2, larghezza, altezza);
        }

        // Ombreggiatura: parametri fissi (blur/offset non regolabili), attiva
        // solo se è stato scelto un colore. Impostata sul context prima di
        // disegnare il testo (dritto o ad arco): shadowOffset/Blur restano
        // validi anche dentro le save/restore per-carattere dell'arco.
        const ombraAttiva = !!opzioni.coloreOmbraTestoSponsor;
        if (ombraAttiva) {
            ctx.shadowColor = opzioni.coloreOmbraTestoSponsor;
            ctx.shadowBlur = dimensioneFont * OMBRA_SPONSOR_BLUR_FRAZIONE;
            ctx.shadowOffsetX = dimensioneFont * OMBRA_SPONSOR_OFFSET_X_FRAZIONE;
            ctx.shadowOffsetY = dimensioneFont * OMBRA_SPONSOR_OFFSET_Y_FRAZIONE;
        }

        if (opzioni.letteringAdArcoTestoSponsor) {
            disegnaTestoAdArco(ctx, testo, cx, cy, dimensioneFont, opzioni);
        } else {
            if (opzioni.coloreContornoTestoSponsor) {
                ctx.lineWidth = Math.max(2, Math.round(dimensioneFont * 0.08));
                ctx.strokeStyle = opzioni.coloreContornoTestoSponsor;
                ctx.strokeText(testo, cx, cy);
            }
            ctx.fillStyle = opzioni.coloreTestoSponsor || "#000000";
            ctx.fillText(testo, cx, cy);
        }

        if (ombraAttiva) {
            ctx.shadowColor = "transparent";
            ctx.shadowBlur = 0;
            ctx.shadowOffsetX = 0;
            ctx.shadowOffsetY = 0;
        }

        ctx.restore();
    }

    /**
     * Compone la maglia sul canvas passato e restituisce una Promise che si
     * risolve quando il disegno è completo.
     *
     * opzioni:
     *   - cartellaAssetTemplate: string  (es. "02" — DivisaTemplateDto.CartellaAsset)
     *   - baseUrlTemplate: string        (es. "/img/divisa/template")
     *   - baseUrlCondivisi: string       (es. "/img/divisa/condivisi")
     *   - colore1, colore2, colore3: string ("#RRGGBB")
     *   - testoSponsor: string|null
     *   - coloreTestoSponsor: string|null
     *   - coloreContornoTestoSponsor: string|null
     *   - coloreSfondoTestoSponsor: string|null
     *   - posizioneTestoSponsor: string       ("Alto"/"Centro"/"Basso", fase 8.3, default "Alto")
     *   - fontTestoSponsor: string             ("Predefinito" o chiave di window.DivisaFonts.MANIFESTO, fase 8.3)
     *   - coloreOmbraTestoSponsor: string|null (fase 8.3)
     *   - dimensioneTestoSponsor: number|null  (percentuale sulla base, es. 130 = +30%; ignorata se autoFitTestoSponsor)
     *   - autoFitTestoSponsor: boolean         (fase 8.3, ha priorità su dimensioneTestoSponsor)
     *   - letteringAdArcoTestoSponsor: boolean (fase 8.3, condizionata alla resa finale — vedi disegnaTestoAdArco)
     */
    async function componiMaglia(canvasDestinazione, opzioni) {
        const urlBase = opzioni.baseUrlTemplate + "/" + opzioni.cartellaAssetTemplate + "/base.png";
        const urlManiche = opzioni.baseUrlTemplate + "/" + opzioni.cartellaAssetTemplate + "/maniche.png";
        const urlColletto = opzioni.baseUrlCondivisi + "/colletto.png";
        const urlOmbre = opzioni.baseUrlCondivisi + "/ombre.png";

        const [imgBase, imgManiche, imgColletto, imgOmbre] = await Promise.all([
            caricaImmagine(urlBase),
            caricaImmagine(urlManiche),
            caricaImmagine(urlColletto),
            caricaImmagine(urlOmbre)
        ]);

        canvasDestinazione.width = DIMENSIONE_CANVAS;
        canvasDestinazione.height = DIMENSIONE_CANVAS;
        const ctx = canvasDestinazione.getContext("2d");
        ctx.clearRect(0, 0, DIMENSIONE_CANVAS, DIMENSIONE_CANVAS);

        ctx.drawImage(tingiSagoma(imgBase, opzioni.colore1), 0, 0, DIMENSIONE_CANVAS, DIMENSIONE_CANVAS);
        ctx.drawImage(tingiSagoma(imgManiche, opzioni.colore2), 0, 0, DIMENSIONE_CANVAS, DIMENSIONE_CANVAS);
        ctx.drawImage(tingiSagoma(imgColletto, opzioni.colore3), 0, 0, DIMENSIONE_CANVAS, DIMENSIONE_CANVAS);

        // Layer condiviso di ombre/pieghe: blend "overlay", non una normale
        // sovrapposizione alpha — vedi piano-divisa-squadra.md sezione 3 per
        // il motivo tecnico (si ritaglia da solo sulla sagoma appena
        // disegnata, funziona su qualunque colore di tinta).
        ctx.globalCompositeOperation = "overlay";
        ctx.drawImage(imgOmbre, 0, 0, DIMENSIONE_CANVAS, DIMENSIONE_CANVAS);
        ctx.globalCompositeOperation = "source-over";

        await disegnaSponsor(ctx, opzioni);
    }

    return {
        componiMaglia,
        caricaImmagine,
        tingiSagoma,
        POSIZIONI_TESTO_SPONSOR
    };
})();
