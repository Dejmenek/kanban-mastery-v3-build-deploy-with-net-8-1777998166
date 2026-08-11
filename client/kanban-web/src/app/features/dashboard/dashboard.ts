import { Component, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BoardService } from '../boards/services/board.service';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateBoardRequest } from '../boards/models/board.models';
import { finalize } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Dialog } from '@angular/cdk/dialog';
import { ConfirmModal } from '../../shared/components/confirm-modal/confirm-modal';
import { extractErrorMessage } from '../../shared/utils/extract-error-message';
import { ErrorMessage } from '../../shared/components/error-message/error-message';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
  imports: [RouterLink, ReactiveFormsModule, ErrorMessage],
})
export class Dashboard {
  dialog = inject(Dialog);
  private boardService = inject(BoardService);
  protected isCreating = signal(false);
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);
  protected createBoardForm = new FormGroup({
    name: new FormControl('', Validators.required),
    description: new FormControl(''),
  });

  protected boardsResource = rxResource({
    stream: () => this.boardService.getAll()
  });

  protected boards = linkedSignal(() => this.boardsResource.value() ?? []);

  protected deleteErrorMessage = signal<string | null>(null);
  protected deletingBoardId = signal<number | null>(null);

  protected onSubmit() {
    if (this.createBoardForm.invalid) {
      this.createBoardForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const request: CreateBoardRequest = {
      name: this.createBoardForm.value.name!,
      description: this.createBoardForm.value.description || null,
    };

    this.boardService
      .createBoard(request)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (board) => {
          this.boards.update(boards => [...boards, board]);
          this.createBoardForm.reset();
          this.isCreating.set(false);
        },
        error: (err: HttpErrorResponse) =>
          this.errorMessage.set(extractErrorMessage(err, 'Could not create board. Please try again.')),
      });
  }

  protected onCancel() {
    this.createBoardForm.reset();
    this.errorMessage.set(null);
    this.isCreating.set(false);
  }

  protected openConfirmDialog(boardId: number) {
    this.deletingBoardId.set(boardId);

    const dialogRef = this.dialog.open<boolean>(ConfirmModal);
    dialogRef.closed.subscribe((isConfirmed) => {
      if (isConfirmed) {
        this.deleteBoard(boardId);
      } else {
        this.deletingBoardId.set(null);
      }
    });
  }

  private deleteBoard(boardId: number) {
    this.deleteErrorMessage.set(null);

    this.boardService
      .deleteBoard(boardId)
      .pipe(finalize(() => this.deletingBoardId.set(null)))
      .subscribe({
        next: () => this.boards.update((boards) => boards.filter((board) => board.id !== boardId)),
        error: (err: HttpErrorResponse) =>
          this.deleteErrorMessage.set(extractErrorMessage(err, 'Could not delete board. Please try again.')),
      });
  }
}
