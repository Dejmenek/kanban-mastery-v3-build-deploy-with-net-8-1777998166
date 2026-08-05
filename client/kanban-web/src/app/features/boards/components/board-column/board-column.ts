import { Component, inject, input, output, signal } from '@angular/core';
import { BoardCard } from '../board-card/board-card';
import { CardResponse, ColumnResponse, CreateCardRequest } from '../../models/board.models';
import { CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { BoardService } from '../../services/board.service';

@Component({
  selector: 'app-board-column',
  templateUrl: './board-column.html',
  styleUrl: './board-column.css',
  imports: [BoardCard, CdkDropList, ReactiveFormsModule],
})
export class BoardColumn {
  column = input.required<ColumnResponse>();
  boardId = input.required<number>();
  dropped = output<CdkDragDrop<CardResponse[]>>();
  cardCreated = output<CardResponse>();
  protected boardService = inject(BoardService);

  protected addCardForm = new FormGroup({
    title: new FormControl('', Validators.required),
    description: new FormControl(''),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);
  protected isAdding = signal(false);

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
      .createCard(this.boardId(), request)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (card) => {
          this.cardCreated.emit(card);
          this.addCardForm.reset();
          this.isAdding.set(false);
        },
        error: (err: HttpErrorResponse) => this.errorMessage.set(this.extractErrorMessage(err)),
      });
  }

  protected onCancel() {
    this.addCardForm.reset();
    this.errorMessage.set(null);
    this.isAdding.set(false);
  }

  private extractErrorMessage(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error.length > 0) return err.error;
    return 'Could not create card. Please try again.';
  }
}
