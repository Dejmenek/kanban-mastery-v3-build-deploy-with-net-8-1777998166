import { Component, effect, inject, input, signal } from '@angular/core';
import { BoardColumn } from '../../components/board-column/board-column';
import { BoardDetailsResponse, UpdateBoardRequest } from '../../models/board.models';
import { BoardService } from '../../services/board.service';
import { CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Dialog, DialogModule } from '@angular/cdk/dialog';
import { InviteModal } from '../../components/invite-modal/invite-modal';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { extractErrorMessage } from '../../../../shared/utils/extract-error-message';
import { ErrorMessage } from '../../../../shared/components/error-message/error-message';

@Component({
  selector: 'app-board-detail',
  templateUrl: './board-detail.html',
  styleUrl: './board-detail.css',
  imports: [BoardColumn, CdkDropListGroup, DialogModule, Avatar, ReactiveFormsModule, ErrorMessage],
})
export class BoardDetail {
  board = input.required<BoardDetailsResponse>();
  protected boardService = inject(BoardService);
  private dialog = inject(Dialog);

  protected isEditingBoard = signal(false);
  protected editErrorMessage = signal<string | null>(null);
  protected isSubmittingEdit = signal(false);
  protected editBoardForm = new FormGroup({
    name: new FormControl('', Validators.required),
    description: new FormControl(''),
  });

  constructor() {
    effect(() => this.boardService.setBoard(this.board()));
  }

  openDialog() {
    this.dialog.open(InviteModal);
  }

  onEditBoard() {
    this.editBoardForm.setValue({
      name: this.boardService.boardName(),
      description: this.boardService.boardDescription() ?? '',
    });
    this.editErrorMessage.set(null);
    this.isEditingBoard.set(true);
  }

  onEditBoardSubmit() {
    if (this.editBoardForm.invalid) {
      this.editBoardForm.markAllAsTouched();
      return;
    }

    this.editErrorMessage.set(null);
    this.isSubmittingEdit.set(true);

    const request: UpdateBoardRequest = {
      name: this.editBoardForm.value.name!,
      description: this.editBoardForm.value.description || null,
    };

    this.boardService
      .updateBoard(request)
      .pipe(finalize(() => this.isSubmittingEdit.set(false)))
      .subscribe({
        next: () => this.isEditingBoard.set(false),
        error: (err: HttpErrorResponse) => this.editErrorMessage.set(extractErrorMessage(err, 'Could not update board. Please try again.')),
      });
  }

  onEditBoardCancel() {
    this.editBoardForm.reset();
    this.editErrorMessage.set(null);
    this.isEditingBoard.set(false);
  }
}
