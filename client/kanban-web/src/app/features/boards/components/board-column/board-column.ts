import { Component, computed, inject, input, signal } from '@angular/core';
import { BoardCard } from '../board-card/board-card';
import { CardResponse, ColumnResponse, CreateCardRequest } from '../../models/board.models';
import { CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { BoardService } from '../../services/board.service';
import { extractErrorMessage } from '../../../../shared/utils/extract-error-message';
import { ErrorMessage } from '../../../../shared/components/error-message/error-message';
import { Dialog } from '@angular/cdk/dialog';
import { ConfirmModal } from '../../../../shared/components/confirm-modal/confirm-modal';

@Component({
  selector: 'app-board-column',
  templateUrl: './board-column.html',
  styleUrl: './board-column.css',
  imports: [BoardCard, CdkDropList, ReactiveFormsModule, ErrorMessage],
})
export class BoardColumn {
  column = input.required<ColumnResponse>();
  private dialog = inject(Dialog);
  private boardService = inject(BoardService);

  protected addCardForm = new FormGroup({
    title: new FormControl('', Validators.required),
    description: new FormControl(''),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);
  protected isAdding = signal(false);
  protected isDeleting = computed(() => this.boardService.deletingColumnIds().has(this.column().id));


  protected onDropped(event: CdkDragDrop<CardResponse[]>): void {
    this.boardService.moveCard(event);
  }

  protected onSubmit() {
    if (this.addCardForm.invalid) {
      this.addCardForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const request: CreateCardRequest = {
      title: this.addCardForm.value.title!,
      description: this.addCardForm.value.description || null,
      columnId: this.column().id,
    };

    this.boardService
      .createCard(request)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.addCardForm.reset();
          this.isAdding.set(false);
        },
        error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Could not create card. Please try again.')),
      });
  }

  protected onCancel() {
    this.addCardForm.reset();
    this.errorMessage.set(null);
    this.isAdding.set(false);
  }

  protected onDeleteClick() {
    const dialogRef = this.dialog.open<boolean>(ConfirmModal);
    dialogRef.closed.subscribe((isConfirmed) => {
      if (isConfirmed) {
        this.boardService.deleteColumn(this.column().id).subscribe();
      }
    });
  }
}
