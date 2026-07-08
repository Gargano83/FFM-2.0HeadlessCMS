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

  // Punto 2 della roadmap (FFM.SquadreRelGiocatori): qui verra' registrato un
  // secondo Custom Element (es. 'dami-ffm-squadra-roster'), riusando lo stesso
  // bundle/licenza. Il vecchio AppComponent/ModalComponent (gestione "Squadra")
  // restano nel repository come riferimento per quel porting, ma non sono
  // piu' bootstrappati da questo entry point.
}).catch((err) => console.error('Errore inizializzazione componenti DAMIHeadlessCMS FFM:', err));
