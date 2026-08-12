import { Component, computed, inject, input, signal } from '@angular/core';
import { BoardCard } from '../board-card/board-card';
import { CardResponse, ColumnResponse, CreateCardRequest, UpdateColumnRequest } from '../../models/board.models';
import { CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
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
  imports: [BoardCard, CdkDropList, CdkDragHandle, ReactiveFormsModule, ErrorMessage],
})
export class BoardColumn {
  column = input.required<ColumnResponse>();
  private dialog = inject(Dialog);
  private boardService = inject(BoardService);

  protected addCardForm = new FormGroup({
    title: new FormControl('', Validators.required),
    description: new FormControl(''),
  });
  protected editForm = new FormGroup({
    title: new FormControl('', Validators.required),
    description: new FormControl(''),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);
  protected isAdding = signal(false);
  protected isEditing = signal(false);
  protected editErrorMessage = signal<string | null>(null);
  protected isSubmittingEdit = signal(false);
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

  protected onEditClick() {
    this.editForm.setValue({
      title: this.column().title,
      description: this.column().description ?? '',
    });
    this.editErrorMessage.set(null);
    this.isEditing.set(true);
  }

  protected onEditSubmit() {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.editErrorMessage.set(null);
    this.isSubmittingEdit.set(true);

    const request: UpdateColumnRequest = {
      title: this.editForm.value.title!,
      description: this.editForm.value.description || null,
    };

    this.boardService
      .updateColumn(this.column().id, request)
      .pipe(finalize(() => this.isSubmittingEdit.set(false)))
      .subscribe({
        next: () => this.isEditing.set(false),
        error: (err: HttpErrorResponse) => this.editErrorMessage.set(extractErrorMessage(err, 'Could not update column. Please try again.')),
      });
  }

  protected onEditCancel() {
    this.editForm.reset();
    this.editErrorMessage.set(null);
    this.isEditing.set(false);
  }
}
