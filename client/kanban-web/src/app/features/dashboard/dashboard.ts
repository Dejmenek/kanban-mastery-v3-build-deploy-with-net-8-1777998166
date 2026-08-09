import { Component, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BoardService } from '../boards/services/board.service';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateBoardRequest } from '../boards/models/board.models';
import { finalize } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
  imports: [RouterLink, ReactiveFormsModule],
})
export class Dashboard {
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
        error: (err: HttpErrorResponse) => this.errorMessage.set(this.extractErrorMessage(err)),
      });
  }

  protected onCancel() {
    this.createBoardForm.reset();
    this.errorMessage.set(null);
    this.isCreating.set(false);
  }

  private extractErrorMessage(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error.length > 0) return err.error;
    return 'Could not create board. Please try again.';
  }
}
