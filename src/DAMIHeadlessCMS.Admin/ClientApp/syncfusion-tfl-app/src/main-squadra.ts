import { createApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { createCustomElement } from '@angular/elements';
import { registerLicense } from '@syncfusion/ej2-base';
import { SquadraRosterComponent } from './app/squadra-roster/squadra-roster.component';

// La chiave di licenza community Syncfusion arriva dalla pagina host (var globale
// impostata server-side, mai hardcoded nel bundle come nella vecchia versione).
const licenseKey = (window as any).DAMI_SYNCFUSION_LICENSE as string | undefined;
if (licenseKey) {
  registerLicense(licenseKey);
}

createApplication({
  providers: [provideHttpClient()]
}).then((appRef) => {
  const squadraElement = createCustomElement(SquadraRosterComponent, { injector: appRef.injector });
  customElements.define('dami-ffm-squadra-roster', squadraElement);
}).catch((err) => console.error('Errore inizializzazione componenti DAMIHeadlessCMS FFM:', err));
