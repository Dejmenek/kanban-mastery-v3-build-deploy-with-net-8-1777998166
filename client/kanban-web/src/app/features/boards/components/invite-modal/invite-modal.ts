import { Component, computed, inject, signal } from '@angular/core';
import { DialogRef } from '@angular/cdk/dialog';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { BoardService } from '../../services/board.service';
import { AddBoardMemberRequest } from '../../models/board.models';
import { extractErrorMessage } from '../../../../shared/utils/extract-error-message';
import { ErrorMessage } from '../../../../shared/components/error-message/error-message';

@Component({
  selector: 'app-invite-modal',
  templateUrl: './invite-modal.html',
  imports: [ReactiveFormsModule, ErrorMessage],
  host: { class: 'modal-panel' },
})
export class InviteModal {
  protected dialogRef = inject<DialogRef<void>>(DialogRef);
  private boardService = inject(BoardService);
  protected inviteForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);
  protected isOffline = computed(() => this.boardService.isOffline());

  protected onSubmit() {
    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const addMemberRequest: AddBoardMemberRequest = {
      email: this.inviteForm.value.email!,
    };

    this.boardService
      .addMember(addMemberRequest)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(),
        error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Could not send invite. Please try again.')),
      });
  }
}
