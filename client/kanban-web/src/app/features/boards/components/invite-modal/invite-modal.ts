import { Component, inject, signal } from '@angular/core';
import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { BoardService } from '../../services/board.service';
import { AddBoardMemberRequest, BoardMemberResponse } from '../../models/board.models';

export interface InviteModalData {
  boardId: number;
}

@Component({
  selector: 'app-invite-modal',
  templateUrl: './invite-modal.html',
  imports: [ReactiveFormsModule],
  host: { class: 'modal-panel' },
})
export class InviteModal {
  private data = inject<InviteModalData>(DIALOG_DATA);
  protected dialogRef = inject<DialogRef<BoardMemberResponse>>(DialogRef);
  private boardService = inject(BoardService);
  protected inviteForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);

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
      .addMember(this.data.boardId, addMemberRequest)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (member) => this.dialogRef.close(member),
        error: (err: HttpErrorResponse) => this.errorMessage.set(this.extractErrorMessage(err)),
      });
  }

  private extractErrorMessage(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error.length > 0) return err.error;
    return 'Could not send invite. Please try again.';
  }
}
