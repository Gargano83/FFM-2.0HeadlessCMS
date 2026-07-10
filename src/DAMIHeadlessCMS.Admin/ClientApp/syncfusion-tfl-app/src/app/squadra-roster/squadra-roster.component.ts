import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import {
  CommandClickEventArgs,
  CommandColumnService,
  CommandModel,
  DetailRowService,
  GridComponent,
  GridModule,
  SortService
} from '@syncfusion/ej2-angular-grids';
import { AutoCompleteComponent, AutoCompleteModule } from '@syncfusion/ej2-angular-dropdowns';

interface InfoSquadra {
  idSquadra: number;
  nomeSquadra: string;
  presidente?: string;
  vicePresidente?: string;
  allenatore?: string;
  durataContrattoAllenatore: number;
  stipendioAllenatore: number;
  tesserati: number;
  inPrestito: number;
  inRosa: number;
  aPrestito: number;
  listaA: number;
  under22InRosa: number;
  rimanenzaStagionePrecedente: number;
  refillRanking: number;
  refillValoreSocieta: number;
  refillStadio: number;
  refillStipendi: number;
  monteStipendiAndata: number;
  monteStipendiRitorno: number;
  bilancioMercato: number;
  fairPlayFinanziario: number;
}

interface GiocatoreSquadra {
  id: number;
  nome: string;
  cognome: string;
  nomeCompleto: string;
  dataDiNascita: string | null;
  ruolo: string | null;
  valoreDiMercato: number | null;
  stipendio: number | null;
  stato: string | null;
  mesi: number;
  u22: boolean;
  eta: number;
}

interface GiocatoreSvincolato {
  id: number;
  nomeCompleto: string;
  ruolo: string | null;
  dataDiNascita: string | null;
  valoreDiMercato: number | null;
  stipendio: number | null;
}

/**
 * Rosa di una squadra, esposta come Custom Element (<dami-ffm-squadra-roster>)
 * indipendente dalla pagina che la ospita — stessi principi del componente
 * Giocatori: nessuna variabile globale, parametri via attributi HTML,
 * chiamate HTTP esplicite (nessun DataManager/WebApiAdaptor), campi allineati
 * al camelCase reale restituito dall'API.
 *
 * Sostituisce il vecchio popup Angular Material (ModalComponent) con una riga
 * di dettaglio espandibile della Grid stessa (detailTemplate): meno
 * dipendenze, stesso risultato funzionale (visualizza/modifica Stato e Mesi
 * di un giocatore in rosa).
 */
@Component({
  selector: 'dami-ffm-squadra-roster',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule, GridModule, AutoCompleteModule],
  providers: [DetailRowService, SortService, CommandColumnService],
  templateUrl: './squadra-roster.component.html',
  styleUrls: ['./squadra-roster.component.css']
})
export class SquadraRosterComponent implements OnInit {
  @Input('api-base-url') apiBaseUrl = '/dami/ffm/api/squadre';
  @Input('idsquadra') idSquadraAttr = '';

  @ViewChild('gridRosa') gridRosa!: GridComponent;
  @ViewChild('autocompleteGiocatore') autocompleteGiocatore!: AutoCompleteComponent;

  public idSquadra = 0;
  public info: InfoSquadra | null = null;
  public rosa: GiocatoreSquadra[] = [];
  public giocatoriSvincolati: GiocatoreSvincolato[] = [];
  public isLoading = true;
  public loadError: string | null = null;

  public readonly statiGiocatore: string[] = ['Lista A', 'Lista A (Pr)', 'No Serie A', 'In prestito', 'Fuori rosa'];
  public readonly autocompleteFields = { value: 'nomeCompleto' };
  public readonly commands: CommandModel[] = [
    { buttonOption: { iconCss: 'e-icons e-delete', cssClass: 'e-flat' } }
  ];

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.idSquadra = Number(this.idSquadraAttr);
    if (!this.idSquadra) {
      this.loadError = 'Attributo "idsquadra" mancante o non valido sul componente.';
      this.isLoading = false;
      return;
    }
    this.loadAll();
  }

  private loadAll(): void {
    this.isLoading = true;
    this.loadError = null;

    this.http.get<InfoSquadra>(`${this.apiBaseUrl}/${this.idSquadra}/info`).subscribe({
      next: (info) => (this.info = info),
      error: () => (this.loadError = 'Errore nel caricamento delle informazioni squadra.')
    });

    this.loadRosa();
    this.loadGiocatoriSvincolati();
    this.isLoading = false;
  }

  private loadRosa(): void {
    this.http.get<GiocatoreSquadra[]>(`${this.apiBaseUrl}/${this.idSquadra}/rosa`).subscribe({
      next: (rosa) => {
        this.rosa = rosa;
        this.gridRosa?.refresh();
      },
      error: () => (this.loadError = 'Errore nel caricamento della rosa.')
    });
  }

  private loadGiocatoriSvincolati(): void {
    this.http.get<GiocatoreSvincolato[]>(`${this.apiBaseUrl}/giocatori-svincolati`).subscribe({
      next: (giocatori) => (this.giocatoriSvincolati = giocatori),
      error: () => (this.loadError = 'Errore nel caricamento dei giocatori svincolati.')
    });
  }

  /** Selezione di un giocatore svincolato dall'autocomplete: lo aggiunge subito alla rosa. */
  onSelectGiocatoreSvincolato(args: { itemData?: GiocatoreSvincolato }): void {
    const giocatore = args?.itemData;
    if (!giocatore) {
      return;
    }

    const payload = { valoreDiMercato: giocatore.valoreDiMercato, stipendio: giocatore.stipendio };
    this.http.post(`${this.apiBaseUrl}/${this.idSquadra}/rosa/${giocatore.id}`, payload).subscribe({
      next: () => {
        this.autocompleteGiocatore.clear();
        this.loadRosa();
        this.loadGiocatoriSvincolati();
      },
      error: () => (this.loadError = "Errore durante l'aggiunta del giocatore alla rosa.")
    });
  }

  /** Click sull'icona "elimina" della colonna comandi: rimuove il giocatore dalla rosa. */
  onCommandClick(args: CommandClickEventArgs): void {
    const giocatore = args.rowData as GiocatoreSquadra;
    if (!giocatore) {
      return;
    }

    if (!window.confirm(`Rimuovere ${giocatore.nomeCompleto} dalla rosa?`)) {
      return;
    }

    this.http.delete(`${this.apiBaseUrl}/${this.idSquadra}/rosa/${giocatore.id}`).subscribe({
      next: () => {
        this.loadRosa();
        this.loadGiocatoriSvincolati();
      },
      error: () => (this.loadError = 'Errore durante la rimozione del giocatore dalla rosa.')
    });
  }

  /** Salvataggio Stato/Mesi dalla riga di dettaglio espansa. */
  salvaDettaglio(giocatore: GiocatoreSquadra): void {
    const payload = { mesi: giocatore.mesi, stato: giocatore.stato };
    this.http.put(`${this.apiBaseUrl}/${this.idSquadra}/rosa/${giocatore.id}`, payload).subscribe({
      next: () => this.loadRosa(),
      error: () => (this.loadError = "Errore durante l'aggiornamento del giocatore.")
    });
  }

  formatCurrency(value: number | null | undefined): string {
    return typeof value === 'number'
      ? new Intl.NumberFormat('it-IT', {
          style: 'currency',
          currency: 'EUR',
          minimumFractionDigits: 3,
          maximumFractionDigits: 3
        }).format(value)
      : '—';
  }
}
