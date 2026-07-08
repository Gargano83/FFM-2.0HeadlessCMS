# syncfusion-tfl-app — Componenti Angular/Syncfusion per DAMIHeadlessCMS (modulo FFM)

Angular 17 + Syncfusion EJ2 (licenza community). Genera **Custom Element**
(Web Component nativi, via `@angular/elements`) montabili con un semplice tag
HTML in qualunque pagina, indipendentemente dal framework che la ospita —
in questo caso le view Razor del backoffice `DAMIHeadlessCMS.Admin`.

## Perché Custom Element e non un'app Angular "intera"

La vecchia integrazione iniettava l'intero `dist/index.html` compilato
(`<html><head><body>` innestati) dentro un'altra pagina, leggendo la
configurazione da una variabile globale `window.appSettings`. Con i Custom
Element, invece:

- Ogni funzionalità è un tag HTML autonomo, es. `<dami-ffm-giocatori-grid>`.
- I parametri sono attributi HTML (`api-base-url="..."`), non variabili globali.
- Nessun conflitto di `<head>`/CSS con la pagina host.

## Componenti disponibili

| Custom Element | Componente Angular | Tabella gestita | Stato |
|---|---|---|---|
| `<dami-ffm-giocatori-grid>` | `GiocatoriComponent` | `FFM.Giocatori` | ✅ Migrato |
| `<dami-ffm-squadra-roster>` | *(da migrare)* | `FFM.SquadreRelGiocatori` | ⏳ Prossimo step |

Il vecchio `AppComponent`/`ModalComponent` (gestione "Squadra") restano nel
repository come riferimento per il prossimo porting, ma non sono più
bootstrappati dall'attuale `main.ts`.

## Percorso nel repository

Questo progetto vive dentro la Razor Class Library che lo consuma:
`src/DAMIHeadlessCMS.Admin/ClientApp/syncfusion-tfl-app/`. È **escluso
esplicitamente** dal `.csproj` di `DAMIHeadlessCMS.Admin` (mai compilato,
pubblicato o incluso in un `dotnet pack`): per MSBuild questa cartella non
esiste. `node_modules/`, `dist/` e `.angular/` sono in `.gitignore` alla
radice del repo — solo il codice sorgente TypeScript/Angular è versionato.

## Build per l'integrazione nel CMS

Il bundle consumato da `DAMIHeadlessCMS.Admin` deve essere **un solo file JS**
(niente chunk separati `runtime`/`polyfills`/`main`, niente CSS esterno):
usiamo [`ngx-build-plus`](https://github.com/manfredsteyer/ngx-build-plus) con
la configurazione dedicata `giocatori-widget` in `angular.json`. Dalla
cartella di questo progetto (`src/DAMIHeadlessCMS.Admin/ClientApp/syncfusion-tfl-app/`):

```
npm install
npm run build:giocatori-widget
```

Output atteso in `dist/ffm-widgets/giocatori/`: un unico `main.js` (bundle
singolo con Angular, Syncfusion e stili inclusi, grazie a `singleBundle` +
`bundleStyles`). Copia quel file in
`../../wwwroot/ffm-widgets/giocatori/main.js` (cioè
`src/DAMIHeadlessCMS.Admin/wwwroot/ffm-widgets/giocatori/main.js` nel
progetto .NET): verrà servito come asset statico della Razor Class Library
all'indirizzo `_content/DAMIHeadlessCMS.Admin/ffm-widgets/giocatori/main.js`.

> Nota: se preferisci mantenere l'output a più file (runtime/polyfills/main
> separati, come nella vecchia build), rimuovi `singleBundle`/`bundleStyles`
> dalla configurazione `giocatori-widget` e aggiorna i tag `<script>` nella
> view Razor di conseguenza.

## Sviluppo locale

```
npm install
npm run start
```

Apre `http://localhost:4200`, con `src/index.html` che monta direttamente
`<dami-ffm-giocatori-grid>` puntato al backend reale in `https://localhost:44341`
(adatta l'URL/porta al tuo `DAMIHeadlessCMS.TestHost` locale). Valorizza anche
`window.DAMI_SYNCFUSION_LICENSE` nello stesso file con la tua chiave community
per i test locali (mai committare la chiave reale).

## Licenza Syncfusion

La chiave di licenza **non è più hardcoded** nel bundle (a differenza della
vecchia `main.ts`): viene letta a runtime da `window.DAMI_SYNCFUSION_LICENSE`,
valorizzata dalla view Razor che ospita il componente a partire dalla
configurazione del backoffice (`DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey`).
