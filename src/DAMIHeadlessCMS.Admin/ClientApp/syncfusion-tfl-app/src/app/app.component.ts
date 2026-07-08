import { Component, isDevMode, OnInit, QueryList, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DataManager, WebApiAdaptor } from '@syncfusion/ej2-data';
import { EditSettingsModel, ExcelExportProperties, GridComponent } from '@syncfusion/ej2-angular-grids';
import { ClickEventArgs, FieldSettingsModel } from '@syncfusion/ej2-angular-navigations';
import * as XLSX from 'xlsx';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AutoCompleteComponent, ListBoxComponent } from '@syncfusion/ej2-angular-dropdowns';
import { MatDialog, MatDialogConfig } from '@angular/material/dialog';
import { ModalComponent } from '../modal/modal.component';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
  @ViewChild('gridGiocatori') gridGiocatori: GridComponent;
  @ViewChild('autocompleteGiocatore') autocompleteGiocatore: AutoCompleteComponent;
  @ViewChild('listSquadraRelGiocatori') listSquadraRelGiocatori: ListBoxComponent;
  public dataGiocatori: DataManager;
  public dataListaGiocatori: Observable<any>;
  public dataSquadraRelGiocatori: DataManager;
  public fieldsSquadraRelGiocatori: FieldSettingsModel = { text: "Id" };
  public giocatoreFields: Object = { value: 'NomeCompleto' };
  public giocatoreWaterMark: string = 'Cerca un giocatore';
  public formatDateOptions: object;
  public editSettings: EditSettingsModel;
  public toolbar: Object[];
  public currencyFormatter = (field: string, data1: any, column: object) => {
    return '€ ' + data1[field];
  }
  public requiredRules: object;
  public appSettings: any;
  public InfoSquadra: any = {};

  constructor(private http: HttpClient, private window: Window, public matDialog: MatDialog) {
    if (isDevMode()) {
      this.window["appSettings"] = { component: 'Squadra', idSquadra: 15 };
    }
  }

  ngOnInit(): void {
    this.appSettings = this.window["appSettings"];
    console.log('this.appSettings ', this.appSettings);
    if (this.appSettings) {
      switch (this.appSettings.component) {
        case ('Giocatori'):
          this.dataGiocatori = new DataManager({
            url: isDevMode() ? 'https://localhost:44341/api/syncfusion/giocatori' : '/api/syncfusion/giocatori',
            adaptor: new WebApiAdaptor()
          });
          this.formatDateOptions = { type: 'date', format: 'dd/MM/yyyy' };
          this.editSettings = { allowEditing: true, allowAdding: true, allowDeleting: true, newRowPosition: 'Bottom' };
          this.requiredRules = { required: true };
          this.toolbar = ['Add', 'Edit', 'Delete', 'Update', 'Cancel', 'Search', 'ExcelExport', 'Excel Import'];
          break;
        case ('Squadra'):
          this.refreshInfoSquadra();
          this.refreshGiocatoriSquadra();
          this.getPlayerFromAPI(this.appSettings.idSquadra);
          break;
      }
    }
  }

  toolbarClick(args: ClickEventArgs): void {
    if (args.item.id?.indexOf('excelexport') != -1) {
      this.gridGiocatori.showSpinner();
      var currentDate = new Date();
      var formatDate = ("00" + currentDate.getDate()).slice(-2) +
        ("00" + (currentDate.getMonth() + 1)).slice(-2) +
        currentDate.getFullYear() + "_" +
        ("00" + currentDate.getHours()).slice(-2) +
        ("00" + currentDate.getMinutes()).slice(-2) +
        ("00" + currentDate.getSeconds()).slice(-2);
      const excelExportProperties: ExcelExportProperties = {
        fileName: 'databaseGiocatori_' + formatDate + '.xlsx',
        includeHiddenColumn: true
      }
      this.gridGiocatori.excelExport(excelExportProperties);
    }
    if (args.item.text === 'Excel Import') {
      document.getElementById('fileBtn')?.click();
    }
  }

  excelExportComplete(): void {
    this.gridGiocatori.hideSpinner();
  }

  importExcelData(event: any): void {
    const file: File = event.target.files[0];
    const reader: FileReader = new FileReader();
    reader.onload = (e: any) => {
      const data: Object[] = this.extractDataFromExcel(e.target.result);
      this.gridGiocatori.showSpinner();
      this.sendDataToAPI(data);
    }
    reader.readAsBinaryString(file);
  }

  extractDataFromExcel(fileData: string): Object[] {
    // Parse the Excel file data and extract the data to an array of objects xlsx library
    const workbook: XLSX.WorkBook = XLSX.read(fileData, { type: 'binary', cellDates: true, cellText: false });
    const worksheet: XLSX.WorkSheet = workbook.Sheets[workbook.SheetNames[0]];
    const data: any[] = XLSX.utils.sheet_to_json(worksheet, { header: 1, raw: false, dateNF: 'dd"/"mm"/"yyyy', blankrows: false });
    //console.log('data ', data);
    // Assuming the first row is the header row and the subsequent rows contain data
    const headers: string[] = data[0];
    const toDate = (dateStr: any) => {
      const [day, month, year] = dateStr.split("/");
      return year + '-' + month + '-' + day + 'T00:00:00';
    }
    return data.slice(1).map((row: any) => {
      const rowData: any = {};
      headers.forEach((header, index) => {
        switch (header) {
          case ('Data di nascita'):
            rowData['DataDiNascita'] = toDate(row[index]);
            break;
          case ('Valore di mercato'):
            rowData['ValoreDiMercato'] = row[index].replace('.', ',');
            break;
          case ('Stipendio'):
            rowData['Stipendio'] = row[index].replace('.', ',');
            break;
          case ('Data aggiornamento'):
            rowData['DataAggiornamento'] = toDate(row[index]);
            break;
          default:
            rowData[header] = row[index];
            break;
        }
      });
      return rowData;
    });
  }

  sendDataToAPI(data: any[]) {
    const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/importgiocatori' : '/api/syncfusion/importgiocatori';
    this.http.post(url, data).subscribe(
      (response) => {
        // Handle the API response here
        this.gridGiocatori.dataSource = response;
        this.gridGiocatori.hideSpinner();
      },
      (error) => {
        // Handle any errors that occur
        console.log('error: ', error);
        this.gridGiocatori.hideSpinner();
      }
    );
  }

  refreshPage() {
    window.location.href = window.location.href;
  }

  refreshInfoSquadra() {
    this.InfoSquadra.Squadra = {};
    this.InfoSquadra.Allenatore = {};
    this.InfoSquadra.Finanze = {};
    this.updateClubToAPI(this.appSettings.idSquadra).subscribe(
      (response) => {
        //console.log('response: ', response);
        this.InfoSquadra.Squadra.Id = response['Squadra']['Id'];
        this.InfoSquadra.Squadra.Nome = response['Squadra']['Nome'];
        this.InfoSquadra.Squadra.Presidente = response['Squadra']['Presidente'];
        this.InfoSquadra.Squadra.VicePresidente = response['Squadra']['VicePresidente'];
        this.InfoSquadra.Allenatore.Nome = response['Allenatore']['Nome'];
        this.InfoSquadra.Allenatore.DurataContratto = response['Allenatore']['DurataContratto'];
        this.InfoSquadra.Allenatore.Stipendio = response['Allenatore']['Stipendio'];
        this.InfoSquadra.Tesserati = response['Tesserati'];
        this.InfoSquadra.InPrestito = response['InPrestito'];
        this.InfoSquadra.InRosa = response['InRosa'];
        this.InfoSquadra.APrestito = response['APrestito'];
        this.InfoSquadra.ListaA = response['ListaA'];
        this.InfoSquadra.Under22InRosa = response['Under22InRosa'];
        this.InfoSquadra.Finanze.RimanenzaStagionePrecedente = response['Finanze']['RimanenzaStagionePrecedente'];
        this.InfoSquadra.Finanze.RefillRanking = response['Finanze']['RefillRanking'];
        this.InfoSquadra.Finanze.RefillValoreSocieta = response['Finanze']['RefillValoreSocieta'];
        this.InfoSquadra.Finanze.RefillStadio = response['Finanze']['RefillStadio'];
        this.InfoSquadra.Finanze.RefillStipendi = response['Finanze']['RefillStipendi'];
        this.InfoSquadra.Finanze.MonteStipendi = response['Finanze']['MonteStipendi'];
        this.InfoSquadra.Finanze.BilancioMercato = response['Finanze']['BilancioMercato'];
        this.InfoSquadra.Finanze.FairPlayFinanziario = response['Finanze']['FairPlayFinanziario'];
      },
      (error) => {
        console.log('error: ', error);
      }
    );
  }

  refreshGiocatoriSquadra() {
    this.dataListaGiocatori = this.http.get<{ [key: string]: object; }[]>(isDevMode() ? 'https://localhost:44341/api/syncfusion/listagiocatori' : '/api/syncfusion/listagiocatori').pipe(
      map((results: { [key: string]: any; }) => {
        return results['Items'];
      })
    );
  }

  onChangeGiocatore(args: any) {
    if (args !== undefined && args.itemData !== null && args.itemData.Id !== undefined) {
      let giocatore: { [key: string]: Object } = { Id: args.itemData.Id, NomeCompleto: args.itemData.NomeCompleto, Ruolo: args.itemData.Ruolo, DataDiNascita: args.itemData.DataDiNascita, ValoreDiMercato: args.itemData.ValoreDiMercato, Stipendio: args.itemData.Stipendio };
      this.sendPlayerToAPI(this.appSettings.idSquadra, giocatore).subscribe(
        (response) => {
          this.listSquadraRelGiocatori.addItem(giocatore);
          this.autocompleteGiocatore.clear();
          this.refreshGiocatoriSquadra();
        },
        (error) => {
          console.log('error: ', error);
        }
      );
    }
  }

  removePlayer(event: any): void {
    let Id = parseInt(event.target.parentElement.parentElement.parentElement.querySelector(".id").innerText);
    let giocatore = this.listSquadraRelGiocatori.getDataByValue(Id);
    if (giocatore !== null) {
      this.updatePlayerToAPI(this.appSettings.idSquadra, giocatore).subscribe(
        (response) => {
          this.listSquadraRelGiocatori.removeItem(giocatore);
        },
        (error) => {
          console.log('error: ', error);
        }
      );
    }
  }

  updateClubToAPI(idSquadra: number) {
    const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/aggiornainfosquadra/' + idSquadra : '/api/syncfusion/aggiornainfosquadra/' + idSquadra;
    return this.http.get(url);
  }

  getPlayerFromAPI(idSquadra: number) {
    this.dataSquadraRelGiocatori = new DataManager({
      url: isDevMode() ? 'https://localhost:44341/api/syncfusion/giocatorisquadra/' + idSquadra : '/api/syncfusion/giocatorisquadra/' + idSquadra,
      adaptor: new WebApiAdaptor()
    });
  }

  sendPlayerToAPI(idSquadra: number, giocatore: any) {
    //console.log('giocatore: ', giocatore);
    const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/aggiungigiocatorepersquadra/' + idSquadra + '/' + giocatore.Id : '/api/syncfusion/aggiungigiocatorepersquadra/' + idSquadra + '/' + giocatore.Id;
    return this.http.post(url, null);
  }

  updatePlayerToAPI(idSquadra: number, giocatore: any) {
    //console.log('giocatore: ', giocatore);
    const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/aggiornagiocatorepersquadra/' + idSquadra + '/' + giocatore.Id : '/api/syncfusion/aggiornagiocatorepersquadra/' + idSquadra + '/' + giocatore.Id;
    return this.http.post(url, null);
  }

  openModal(giocatore: any) {
    const dialogConfig = new MatDialogConfig();
    dialogConfig.disableClose = true;
    dialogConfig.id = "modal-component";
    dialogConfig.height = "auto";
    dialogConfig.width = "600px";
    dialogConfig.panelClass = "detail-player";
    dialogConfig.data = { IdSquadra: this.appSettings.idSquadra, IdGiocatore: giocatore.Id };
    const modalDialog = this.matDialog.open(ModalComponent, dialogConfig).afterClosed().subscribe(() => this.refreshPage());
  }
}
