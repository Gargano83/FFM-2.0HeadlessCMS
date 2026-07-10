// zone.js viene incluso tramite l'array "polyfills" di angular.json (produce
// il chunk separato polyfills.js nella build standard a 3 file). NON importarlo
// qui esplicitamente: un import diretto duplicherebbe lo stesso modulo tra
// l'entry "main" e l'entry "polyfills", inducendo webpack a considerare
// quest'ultimo privo di contenuto univoco e a non emetterlo affatto — è
// esattamente quello che è successo quando questo import era stato aggiunto
// come workaround (ormai superato) per NG0908 durante i tentativi con
// ngx-build-plus --single-bundle.
import { createApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { createCustomElement } from '@angular/elements';
import { registerLicense } from '@syncfusion/ej2-base';
import { GiocatoriComponent } from './app/giocatori/giocatori.component';

// La chiave di licenza community Syncfusion arriva dalla pagina host (var globale
// impostata server-side, mai hardcoded nel bundle come nella vecchia versione).
const licenseKey = (window as any).DAMI_SYNCFUSION_LICENSE as string | undefined;
if (licenseKey) {
  registerLicense(licenseKey);
}

createApplication({
  providers: [provideHttpClient()]
}).then((appRef) => {
  const giocatoriElement = createCustomElement(GiocatoriComponent, { injector: appRef.injector });
  customElements.define('dami-ffm-giocatori-grid', giocatoriElement);
}).catch((err) => console.error('Errore inizializzazione componenti DAMIHeadlessCMS FFM:', err));
