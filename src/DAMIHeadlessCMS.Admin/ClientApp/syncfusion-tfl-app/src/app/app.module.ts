import { NgModule } from '@angular/core';
import { HttpClientModule } from '@angular/common/http';
import { BrowserModule } from '@angular/platform-browser';
import { GridModule, EditService, ExcelExportService, SearchService, ToolbarService } from '@syncfusion/ej2-angular-grids';
import { AutoCompleteModule, ListBoxModule } from '@syncfusion/ej2-angular-dropdowns';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { APP_BASE_HREF } from '@angular/common';

import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { ModalComponent as ModalComponent } from '../modal/modal.component';
import {MatIconModule} from '@angular/material/icon';

import { ReactiveFormsModule } from '@angular/forms';

@NgModule({
  declarations: [
    AppComponent,
    ModalComponent
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    GridModule,
    AutoCompleteModule,
    ListBoxModule,
    BrowserAnimationsModule,
    MatButtonModule,
    MatDialogModule,
    ReactiveFormsModule,
    MatIconModule 
  ],
  providers: [{ provide: APP_BASE_HREF, useValue: '/syncfusion-tfl-app/dist/' }, { provide: Window, useValue: window }, EditService, ExcelExportService, SearchService, ToolbarService],
  bootstrap: [AppComponent],
  entryComponents: [ModalComponent]
})
export class AppModule { }
