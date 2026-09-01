// Wiring della pagina di validazione tecnica del motore di rendering divisa
// (Views/DivisaEngine/Index.cshtml). Codice di solo sviluppo/QA: legge
// CATALOGO_TEMPLATE (iniettato inline dalla view) e richiama
// DivisaRenderEngine.componiMaglia ad ogni cambio di template/colore/sponsor.
// Nessuna chiamata di salvataggio: questa fase valida solo il motore, la
// persistenza è materia della fase 5 del piano.
(function () {
    "use strict";

    const BASE_URL_TEMPLATE = "/img/divisa/template";
    const BASE_URL_CONDIVISI = "/img/divisa/condivisi";

    const galleriaEl = document.getElementById("templateGallery");
    const canvasEl = document.getElementById("anteprimaCanvas");
    const erroreEl = document.getElementById("erroreMotore");
    const labelEl = document.getElementById("templateSelezionatoLabel");

    const colore1El = document.getElementById("colore1");
    const colore2El = document.getElementById("colore2");
    const colore3El = document.getElementById("colore3");
    const testoSponsorEl = document.getElementById("testoSponsor");
    const coloreTestoSponsorEl = document.getElementById("coloreTestoSponsor");
    const coloreContornoTestoSponsorEl = document.getElementById("coloreContornoTestoSponsor");
    const coloreSfondoTestoSponsorEl = document.getElementById("coloreSfondoTestoSponsor");
    const usaContornoSponsorEl = document.getElementById("usaContornoSponsor");
    const usaSfondoSponsorEl = document.getElementById("usaSfondoSponsor");

    if (!galleriaEl || typeof CATALOGO_TEMPLATE === "undefined" || CATALOGO_TEMPLATE.length === 0) {
        return;
    }

    let templateSelezionato = CATALOGO_TEMPLATE[0];

    function renderGalleria() {
        galleriaEl.innerHTML = CATALOGO_TEMPLATE.map(function (t) {
            return (
                '<div class="col">' +
                '<button type="button" class="btn btn-outline-secondary p-1 w-100 template-thumb" ' +
                'data-id="' + t.id + '" title="' + t.nome + ' (' + t.cartellaAsset + ')">' +
                '<img src="' + BASE_URL_TEMPLATE + "/" + t.cartellaAsset + '/icona.png" class="img-fluid" alt="' + t.nome + '">' +
                "</button>" +
                "</div>"
            );
        }).join("");

        const bottoni = galleriaEl.querySelectorAll(".template-thumb");
        bottoni.forEach(function (btn) {
            btn.addEventListener("click", function () {
                const trovato = CATALOGO_TEMPLATE.find(function (t) { return String(t.id) === btn.dataset.id; });
                if (!trovato) {
                    return;
                }
                templateSelezionato = trovato;
                bottoni.forEach(function (b) { b.classList.remove("btn-secondary", "active"); });
                btn.classList.add("btn-secondary", "active");
                aggiorna();
            });
        });

        if (bottoni.length > 0) {
            bottoni[0].classList.add("btn-secondary", "active");
        }
    }

    async function aggiorna() {
        if (!templateSelezionato) {
            return;
        }

        labelEl.textContent = templateSelezionato.nome + " (cartella " + templateSelezionato.cartellaAsset + ")";
        erroreEl.classList.add("d-none");

        try {
            await DivisaRenderEngine.componiMaglia(canvasEl, {
                cartellaAssetTemplate: templateSelezionato.cartellaAsset,
                baseUrlTemplate: BASE_URL_TEMPLATE,
                baseUrlCondivisi: BASE_URL_CONDIVISI,
                colore1: colore1El.value,
                colore2: colore2El.value,
                colore3: colore3El.value,
                testoSponsor: testoSponsorEl.value,
                coloreTestoSponsor: coloreTestoSponsorEl.value,
                coloreContornoTestoSponsor: usaContornoSponsorEl.checked ? coloreContornoTestoSponsorEl.value : null,
                coloreSfondoTestoSponsor: usaSfondoSponsorEl.checked ? coloreSfondoTestoSponsorEl.value : null
            });
        } catch (err) {
            erroreEl.textContent = "Errore nella composizione: " + err.message;
            erroreEl.classList.remove("d-none");
        }
    }

    [
        colore1El, colore2El, colore3El,
        testoSponsorEl, coloreTestoSponsorEl, coloreContornoTestoSponsorEl, coloreSfondoTestoSponsorEl,
        usaContornoSponsorEl, usaSfondoSponsorEl
    ].forEach(function (el) {
        el.addEventListener("input", aggiorna);
    });

    renderGalleria();
    aggiorna();
})();
