import { Component, isDevMode, Inject, OnInit, Optional } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormGroup, FormBuilder, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

@Component({
  selector: 'app-modal',
  templateUrl: './modal.component.html',
  styleUrls: ['./modal.component.css']
})

export class ModalComponent implements OnInit {
  public IdSquadra: number;
  public IdGiocatore: number;
  public NomeGiocatore: string;
  public dettaglioGiocatoreForm: FormGroup;
  public statiGiocatore: string[];
  public messageOk: boolean = false;
  public messageKo: boolean = false;

  constructor(private http: HttpClient, public fb: FormBuilder, public dialogRef: MatDialogRef<ModalComponent>, @Optional() @Inject(MAT_DIALOG_DATA) public data: any) {
    this.IdSquadra = data.IdSquadra;
    this.IdGiocatore = data.IdGiocatore;
    this.statiGiocatore = ['', 'Lista A', 'Lista A (Pr)', 'No Serie A', 'In prestito', 'Fuori rosa'];
    this.dettaglioGiocatoreForm = this.fb.group({
      Nome: [{ value: '', disabled: true }],
      Cognome: [{ value: '', disabled: true }],
      Ruolo: [{ value: '', disabled: true }],
      DataDiNascita: [{ value: '', disabled: true }],
      ValoreDiMercato: [{ value: '', disabled: true }, Validators.required],
      Stipendio: [{ value: '', disabled: true }, Validators.required],
      Stato: [{ value: '' }, Validators.required],
      Mesi: [{ value: '' }, Validators.required]
    });
  }

  ngOnInit() {
    //console.log('this.IdSquadra ', this.IdSquadra);
    //console.log('this.IdGiocatore ', this.IdGiocatore);
    this.getDetailPlayerFromAPI().subscribe(
      (response) => {
        //console.log('response: ', response);
        this.NomeGiocatore = response['NomeCompleto'];
        this.dettaglioGiocatoreForm.patchValue({
          Nome: response['Nome'],
          Cognome: response['Cognome'],
          Ruolo: response['Ruolo'],
          DataDiNascita: response['DataDiNascitaFormat'],
          ValoreDiMercato: response['ValoreDiMercato'],
          Stipendio: response['Stipendio'],
          Stato: response['Stato'],
          Mesi: response['Mesi']
        });
      },
      (error) => {
        console.log('error: ', error);
      }
    );
  }

  getDetailPlayerFromAPI() {
    const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/dettagliogiocatorepersquadra/' + this.IdSquadra + '/' + this.IdGiocatore : '/api/syncfusion/dettagliogiocatorepersquadra/' + this.IdSquadra + '/' + this.IdGiocatore;
    return this.http.get(url);
  }

  onSubmit() {
    if (this.dettaglioGiocatoreForm.valid) {
      //console.log('Form valida: ', this.dettaglioGiocatoreForm.value);
      const url = isDevMode() ? 'https://localhost:44341/api/syncfusion/aggiornadettagliogiocatorepersquadra' : '/api/syncfusion/aggiornadettagliogiocatorepersquadra';
      let giocatore = {
        IdGiocatore: this.IdGiocatore,
        IdSquadra: this.IdSquadra,
        Mesi: this.dettaglioGiocatoreForm.value.Mesi,
        Stato: this.dettaglioGiocatoreForm.value.Stato,
        Stipendio: this.dettaglioGiocatoreForm.value.Stipendio,
        ValoreDiMercato: this.dettaglioGiocatoreForm.value.ValoreDiMercato
      }
      this.http.post(url, giocatore).subscribe(
        (response) => {
          //console.log('response: ', response);
          this.messageKo = false;
          this.messageOk = true;
        },
        (error) => {
          //console.log('error: ', error);
          this.messageOk = false;
          this.messageKo = true;
        }
      );
    }
  }

  closeModal() {
    this.dialogRef.close();
  }
}
