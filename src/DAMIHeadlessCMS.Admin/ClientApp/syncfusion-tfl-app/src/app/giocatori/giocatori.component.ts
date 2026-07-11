import { Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import {
  EditService,
  EditSettingsModel,
  ExcelExportProperties,
  ExcelExportService,
  GridComponent,
  GridModule,
  SearchService,
  SortService,
  ToolbarService
} from '@syncfusion/ej2-angular-grids';
import { ClickEventArgs } from '@syncfusion/ej2-angular-navigations';
import * as XLSX from 'xlsx';

/**
 * Griglia CRUD su FFM.Giocatori, esposta come Custom Element
 * (<dami-ffm-giocatori-grid>) indipendente dalla pagina che la ospita.
 *
 * Differenze rispetto alla vecchia implementazione (AppComponent):
 * - Nessuna dipendenza da `window.appSettings`: l'unico parametro
 *   (endpoint API) arriva come attributo HTML (`api-base-url`).
 * - Nessun DataManager/WebApiAdaptor: il caricamento dati e le operazioni
 *   CRUD sono esplicite via HttpClient, per non dipendere dalle convenzioni
 *   URL di un adaptor specifico e restare facilmente debuggabili.
 * - I valori numerici (ValoreDiMercato/Stipendio) sono number nativi, non
 *   più stringhe formattate "all'italiana" da riparsare lato server.
 */
@Component({
  selector: 'dami-ffm-giocatori-grid',
  standalone: true,
  imports: [CommonModule, HttpClientModule, GridModule],
  providers: [ToolbarService, EditService, ExcelExportService, SearchService, SortService],
  templateUrl: './giocatori.component.html',
  styleUrls: ['./giocatori.component.css']
})
export class GiocatoriComponent implements OnInit {
  /** Endpoint REST del backoffice DAMIHeadlessCMS per il CRUD Giocatori. */
  @Input('api-base-url') apiBaseUrl = '/dami/ffm/api/giocatori';

  /**
   * Sola lettura per CmsOperator: arriva come attributo HTML ("true"/"false",
   * i Custom Element non hanno un vero tipo boolean). Disabilita editing,
   * aggiunta, cancellazione e import; la vera barriera di sicurezza resta
   * comunque lato server (le API di scrittura richiedono CmsAdmin).
   */
  @Input('read-only') readOnlyAttr = 'false';
  public isReadOnly = false;

  @ViewChild('gridGiocatori') gridGiocatori!: GridComponent;
  @ViewChild('fileInput') fileInputRef!: ElementRef<HTMLInputElement>;

  public dataGiocatori: Record<string, unknown>[] = [];
  public isLoading = true;
  public loadError: string | null = null;

  public formatDateOptions: object = { type: 'date', format: 'dd/MM/yyyy' };
  public editSettings: EditSettingsModel = {
    allowEditing: true,
    allowAdding: true,
    allowDeleting: true,
    newRowPosition: 'Bottom'
  };
  public toolbar: (string | object)[] = ['Add', 'Edit', 'Delete', 'Update', 'Cancel', 'Search', 'ExcelExport', 'Excel Import'];
  public requiredRules: object = { required: true };

  public currencyFormatter = (field: string, data: Record<string, unknown>) => {
    const value = data[field];
    return typeof value === 'number'
      ? new Intl.NumberFormat('it-IT', {
          style: 'currency',
          currency: 'EUR',
          minimumFractionDigits: 3,
          maximumFractionDigits: 3
        }).format(value)
      : '';
  };

  /** Editor NumericTextBox (colonne ValoreDiMercato/Stipendio) a 3 decimali. */
  public numericEditParams: object = { params: { decimals: 3, format: 'n3' } };

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.isReadOnly = this.readOnlyAttr === 'true';
    if (this.isReadOnly) {
      this.editSettings = { allowEditing: false, allowAdding: false, allowDeleting: false };
      this.toolbar = ['Search', 'ExcelExport'];
    }
    this.loadGiocatori();
  }

  private loadGiocatori(): void {
    this.isLoading = true;
    this.loadError = null;
    this.http.get<Record<string, unknown>[]>(this.apiBaseUrl).subscribe({
      next: (data) => {
        this.dataGiocatori = data;
        this.isLoading = false;
        // Riassegnare dataSource non basta a far ricalcolare le colonne con
        // valueAccessor (es. ValoreDiMercato/Stipendio): senza un refresh
        // esplicito restano vuote finché non si entra/esce dalla modalità
        // di modifica di quella specifica riga. ViewChild potrebbe non
        // essere ancora popolato al primissimo caricamento (ngOnInit corre
        // prima di ngAfterViewInit), da qui l'optional chaining.
        this.gridGiocatori?.refresh();
      },
      error: () => {
        this.loadError = 'Errore nel caricamento dei giocatori. Riprova più tardi.';
        this.isLoading = false;
      }
    });
  }

  toolbarClick(args: ClickEventArgs): void {
    if (args.item?.id?.toLowerCase().indexOf('excelexport') !== -1) {
      this.exportToExcel();
    }
    if (args.item?.text === 'Excel Import') {
      this.fileInputRef.nativeElement.click();
    }
  }

  private exportToExcel(): void {
    this.gridGiocatori.showSpinner();
    const now = new Date();
    const pad = (n: number) => n.toString().padStart(2, '0');
    const fileName =
      `databaseGiocatori_${pad(now.getDate())}${pad(now.getMonth() + 1)}${now.getFullYear()}_` +
      `${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}.xlsx`;
    const excelExportProperties: ExcelExportProperties = { fileName, includeHiddenColumn: true };
    this.gridGiocatori.excelExport(excelExportProperties);
  }

  excelExportComplete(): void {
    this.gridGiocatori.hideSpinner();
  }

  /**
   * Con dataSource ad array locale, la Grid applica già l'operazione in
   * memoria: qui la persistiamo lato server e poi ricarichiamo l'intero
   * elenco, così la UI resta sempre coerente con il database (data set di
   * dimensioni contenute: un reload completo per operazione è accettabile
   * e molto più semplice/robusto che sincronizzare id/righe manualmente).
   */
  actionComplete(args: any): void {
    if (args.requestType !== 'save' && args.requestType !== 'delete') {
      return;
    }

    if (args.requestType === 'save' && args.action === 'add') {
      this.http.post(this.apiBaseUrl, this.toPayload(args.data)).subscribe({
        next: () => this.loadGiocatori(),
        error: () => (this.loadError = 'Errore durante il salvataggio del nuovo giocatore.')
      });
    } else if (args.requestType === 'save' && args.action === 'edit') {
      // args.data e args.previousData usano le chiavi camelCase reali
      // restituite dall'API (default ASP.NET Core), non PascalCase.
      // previousData è la riga COM'ERA prima della modifica, fornita da
      // Syncfusion apposta per recuperare in modo affidabile la chiave
      // primaria di una colonna nascosta ([visible]="false").
      const id = args.previousData?.id ?? args.data?.id;
      this.http.put(`${this.apiBaseUrl}/${id}`, this.toPayload(args.data)).subscribe({
        next: () => this.loadGiocatori(),
        error: () => (this.loadError = 'Errore durante l\'aggiornamento del giocatore.')
      });
    } else if (args.requestType === 'delete') {
      const rows: Record<string, unknown>[] = args.data;
      rows.forEach((row) => {
        this.http.delete(`${this.apiBaseUrl}/${row['id']}`).subscribe({
          error: () => (this.loadError = 'Errore durante la cancellazione del giocatore.')
        });
      });
      // Piccolo delay per dare tempo alle DELETE di completare prima del reload.
      setTimeout(() => this.loadGiocatori(), 300);
    }
  }

  private toPayload(row: Record<string, unknown>) {
    return {
      id: row['id'] ?? 0,
      nome: row['nome'],
      cognome: row['cognome'],
      dataDiNascita: row['dataDiNascita'],
      ruolo: row['ruolo'],
      valoreDiMercato: row['valoreDiMercato'],
      stipendio: row['stipendio'],
      dataAggiornamento: row['dataAggiornamento'],
      note: row['note']
    };
  }

  importExcelData(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const data = this.extractDataFromExcel((e.target as FileReader).result as string);
      this.gridGiocatori.showSpinner();

      const confirmed = window.confirm(
        'ATTENZIONE: l\'import sincronizza l\'intero database giocatori con il file Excel. ' +
          'I giocatori NON presenti nel file verranno ELIMINATI definitivamente. Continuare?'
      );
      if (!confirmed) {
        this.gridGiocatori.hideSpinner();
        input.value = '';
        return;
      }

      this.http.post<Record<string, unknown>[]>(`${this.apiBaseUrl}/import`, data).subscribe({
        next: (response) => {
          this.dataGiocatori = response;
          this.gridGiocatori.hideSpinner();
        },
        error: () => {
          this.loadError = 'Errore durante l\'import da Excel.';
          this.gridGiocatori.hideSpinner();
        }
      });
      input.value = '';
    };
    reader.readAsBinaryString(file);
  }

  private extractDataFromExcel(fileData: string): Record<string, unknown>[] {
    const workbook = XLSX.read(fileData, { type: 'binary', cellDates: true, cellText: false });
    const worksheet = workbook.Sheets[workbook.SheetNames[0]];
    const rows: unknown[][] = XLSX.utils.sheet_to_json(worksheet, {
      header: 1,
      raw: false,
      dateNF: 'dd"/"mm"/"yyyy',
      blankrows: false
    });

    const headers = rows[0] as string[];
    const toIsoDate = (value: string): string => {
      const [day, month, year] = value.split('/');
      return `${year}-${month}-${day}T00:00:00`;
    };
    const toNumber = (value: string): number => Number(String(value).replace('.', '').replace(',', '.'));

    return rows.slice(1).map((row) => {
      const rowData: Record<string, unknown> = {};
      headers.forEach((header, index) => {
        const cell = row[index];
        switch (header) {
          case 'Id':
            rowData['id'] = cell ? Number(cell) : 0;
            break;
          case 'Nome':
            rowData['nome'] = cell;
            break;
          case 'Cognome':
            rowData['cognome'] = cell;
            break;
          case 'Data di nascita':
            rowData['dataDiNascita'] = cell ? toIsoDate(String(cell)) : null;
            break;
          case 'Ruolo':
            rowData['ruolo'] = cell;
            break;
          case 'Valore di mercato':
            rowData['valoreDiMercato'] = cell ? toNumber(String(cell)) : null;
            break;
          case 'Stipendio':
            rowData['stipendio'] = cell ? toNumber(String(cell)) : null;
            break;
          case 'Data aggiornamento':
            rowData['dataAggiornamento'] = cell ? toIsoDate(String(cell)) : null;
            break;
          case 'Note':
            rowData['note'] = cell;
            break;
        }
      });
      return rowData;
    });
  }
}
