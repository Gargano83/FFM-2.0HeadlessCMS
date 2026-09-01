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
     * Disegna il testo sponsor centrato sul petto. Contorno e sfondo sono
     * facoltativi (null/undefined per disattivarli) — coerente con
     * ColoreContornoTestoSponsor/ColoreSfondoTestoSponsor, nullable nel DTO.
     */
    function disegnaSponsor(ctx, opzioni) {
        const testo = (opzioni.testoSponsor || "").trim();
        if (!testo) {
            return;
        }

        const dimensioneFont = Math.round(DIMENSIONE_CANVAS * 0.066); // ~34px a 512
        const cx = DIMENSIONE_CANVAS / 2;
        const cy = DIMENSIONE_CANVAS * 0.55; // sotto il collo, sopra il fondo maglia

        ctx.save();
        ctx.font = "700 " + dimensioneFont + "px system-ui, -apple-system, Segoe UI, Roboto, sans-serif";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";

        if (opzioni.coloreSfondoTestoSponsor) {
            const metriche = ctx.measureText(testo);
            const paddingX = dimensioneFont * 0.4;
            const paddingY = dimensioneFont * 0.3;
            const larghezza = metriche.width + paddingX * 2;
            const altezza = dimensioneFont + paddingY * 2;
            ctx.fillStyle = opzioni.coloreSfondoTestoSponsor;
            ctx.fillRect(cx - larghezza / 2, cy - altezza / 2, larghezza, altezza);
        }

        if (opzioni.coloreContornoTestoSponsor) {
            ctx.lineWidth = Math.max(2, Math.round(dimensioneFont * 0.08));
            ctx.strokeStyle = opzioni.coloreContornoTestoSponsor;
            ctx.strokeText(testo, cx, cy);
        }

        ctx.fillStyle = opzioni.coloreTestoSponsor || "#000000";
        ctx.fillText(testo, cx, cy);
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

        disegnaSponsor(ctx, opzioni);
    }

    return {
        componiMaglia,
        caricaImmagine,
        tingiSagoma
    };
})();
