// Caricamento dei font brandizzati per il lettering sponsor della divisa
// (vedi piano-divisa-squadra.md, sezione 8, punto 2 — fase 8.2).
//
// A differenza di divisa-render-engine.js (che vive nel wwwroot dell'host
// consumer e va copiato manualmente), questo file vive nel wwwroot della
// Razor Class Library DAMIHeadlessCMS.Admin insieme ai 5 file .woff2: viaggia
// quindi automaticamente via NuGet (static web assets) in ogni host che
// referenzia il pacchetto, servito su ~/_content/DAMIHeadlessCMS.Admin/fonts/divisa/.
// Nessun host deve copiare manualmente né i font né questo loader.
//
// Il modulo calcola da solo, a runtime, la cartella in cui si trova (dal
// proprio <script src>, via document.currentScript) e la usa per risolvere i
// percorsi dei file .woff2 accanto a sé: il motore di rendering (fase 8.3)
// non deve conoscere né passare alcun baseUrl per i font, a differenza degli
// asset immagine del template (base.png/maniche.png/ecc., che invece restano
// nel wwwroot dell'host e richiedono un baseUrl esplicito).
window.DivisaFonts = (function () {
    "use strict";

    const TIMEOUT_MS = 2000;
    const FALLBACK_FAMILY = "system-ui, -apple-system, Segoe UI, Roboto, sans-serif";

    // Manifesto font brandizzati: chiave (valore del campo FontTestoSponsor) ->
    // file/peso/famiglia CSS da registrare. Unica fonte di verità per questa
    // mappatura — il motore di rendering e l'eventuale UI di selezione (fase
    // 8.3/8.4) leggono le chiavi da qui, nessun elenco duplicato altrove.
    const MANIFESTO = {
        Anton: { file: "anton.woff2", peso: "400", famiglia: "DivisaAnton" },
        BebasNeue: { file: "bebas-neue.woff2", peso: "400", famiglia: "DivisaBebasNeue" },
        Oswald: { file: "oswald.woff2", peso: "700", famiglia: "DivisaOswald" },
        RussoOne: { file: "russo-one.woff2", peso: "400", famiglia: "DivisaRussoOne" },
        Teko: { file: "teko.woff2", peso: "700", famiglia: "DivisaTeko" }
    };

    // Cartella di questo stesso <script>, risolta una sola volta al parsing
    // (document.currentScript è valido solo in modo sincrono durante
    // l'esecuzione di uno <script> classico, non async/defer/module — questo
    // file va quindi incluso con un tag <script src="..."> semplice, come
    // divisa-render-engine.js).
    const cartellaScript = (function () {
        const src = document.currentScript && document.currentScript.src;
        return src ? new URL(".", src).href : "";
    })();

    // chiave font -> Promise<string> (la famiglia CSS effettiva da usare in
    // ctx.font, già risolta col fallback in caso di errore) — cache per non
    // ripetere il caricamento di rete ad ogni ridisegno dell'anteprima live.
    const cache = new Map();

    function attendiConTimeout(promise, ms) {
        return Promise.race([
            promise,
            new Promise((_, reject) => setTimeout(() => reject(new Error("timeout caricamento font")), ms))
        ]);
    }

    /**
     * Garantisce che il font brandizzato richiesto sia caricato e pronto per
     * essere usato in un ctx.font di Canvas 2D, restituendo la stringa
     * famiglia CSS completa (col fallback già incluso in coda) da assegnare
     * direttamente a ctx.font. Non lancia mai eccezioni: chiave sconosciuta,
     * "Predefinito", errore di rete o timeout producono tutti lo stesso
     * risultato sicuro (il solo fallback) — il chiamante non deve gestire un
     * ramo di errore separato, il rendering procede comunque.
     */
    function caricaFont(chiaveFont) {
        if (!chiaveFont || chiaveFont === "Predefinito" || !MANIFESTO[chiaveFont]) {
            return Promise.resolve(FALLBACK_FAMILY);
        }

        if (cache.has(chiaveFont)) {
            return cache.get(chiaveFont);
        }

        const voce = MANIFESTO[chiaveFont];
        const promessa = (async () => {
            try {
                if (!cartellaScript) {
                    throw new Error("impossibile determinare la cartella di divisa-fonts.js (document.currentScript non disponibile)");
                }
                const url = new URL(voce.file, cartellaScript).href;
                const fontFace = new FontFace(voce.famiglia, `url(${url})`, { weight: voce.peso });
                await attendiConTimeout(fontFace.load(), TIMEOUT_MS);
                document.fonts.add(fontFace);
                return `"${voce.famiglia}", ${FALLBACK_FAMILY}`;
            } catch (errore) {
                console.warn("[DivisaFonts] Font \"" + chiaveFont + "\" non caricato, uso il font predefinito:", errore);
                return FALLBACK_FAMILY;
            }
        })();

        cache.set(chiaveFont, promessa);
        return promessa;
    }

    return {
        MANIFESTO,
        caricaFont
    };
})();
