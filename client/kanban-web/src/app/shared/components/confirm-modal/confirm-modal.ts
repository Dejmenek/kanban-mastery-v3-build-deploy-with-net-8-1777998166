import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';

export interface ConfirmModalData {
  itemToDeleteId: number;
}

@Component({
  selector: 'app-confirm-modal',
  templateUrl: './confirm-modal.html',
  host: { class: 'modal-panel' },
})
export class ConfirmModal {
  protected dialogRef = inject<DialogRef<boolean>>(DialogRef);

  confirm(isConfirmed: boolean) {
    this.dialogRef.close(isConfirmed);
  }
}
