import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { BoardColumn } from '../../components/board-column/board-column';
import {
  AffectedColumnResponse,
  BoardDetailsResponse,
  BoardMemberResponse,
  CardResponse,
  ColumnResponse,
  MoveCardRequest,
  UpdateBoardRequest,
} from '../../models/board.models';
import { BoardService } from '../../services/board.service';
import { CdkDragDrop, CdkDropListGroup, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { Dialog, DialogModule } from '@angular/cdk/dialog';
import { InviteModal } from '../../components/invite-modal/invite-modal';
import { ConfirmModal } from '../../../../shared/components/confirm-modal/confirm-modal';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-board-detail',
  templateUrl: './board-detail.html',
  styleUrl: './board-detail.css',
  imports: [BoardColumn, CdkDropListGroup, DialogModule, Avatar, ReactiveFormsModule],
})
export class BoardDetail {
  board = input.required<BoardDetailsResponse>();
  dialog = inject(Dialog);

  private boardService = inject(BoardService);
  private columnGeneration = new Map<number, number>();

  protected columns = linkedSignal(() => this.cloneColumns(this.board().columns));
  protected members = linkedSignal(() => this.board().members);
  protected boardName = linkedSignal(() => this.board().name);
  protected boardDescription = linkedSignal(() => this.board().description);
  protected moveError = signal<string | null>(null);
  protected isOwner = computed(() => this.board().userRole === 'Owner');

  protected deletingCardId = signal<number | null>(null);
  protected deleteCardError = signal<string | null>(null);

  protected isEditingBoard = signal(false);
  protected editErrorMessage = signal<string | null>(null);
  protected isSubmittingEdit = signal(false);
  protected editBoardForm = new FormGroup({
    name: new FormControl('', Validators.required),
    description: new FormControl(''),
  });

  openDialog() {
    const dialogRef = this.dialog.open<BoardMemberResponse>(InviteModal, { data: { boardId: this.board().id } });
    dialogRef.closed.subscribe((member) => {
      if (member) this.members.update((current) => [...current, member]);
    });
  }

  onEditBoard() {
    this.editBoardForm.setValue({ name: this.boardName(), description: this.boardDescription() ?? '' });
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
      .updateBoard(this.board().id, request)
      .pipe(finalize(() => this.isSubmittingEdit.set(false)))
      .subscribe({
        next: (updated) => {
          this.boardName.set(updated.name);
          this.boardDescription.set(updated.description);
          this.isEditingBoard.set(false);
        },
        error: (err: HttpErrorResponse) => this.editErrorMessage.set(this.extractErrorMessage(err)),
      });
  }

  onEditBoardCancel() {
    this.editBoardForm.reset();
    this.editErrorMessage.set(null);
    this.isEditingBoard.set(false);
  }

  private extractErrorMessage(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error.length > 0) return err.error;
    return 'Could not update board. Please try again.';
  }

  onCardCreated(columnId: number, card: CardResponse): void {
    this.columns.update((cols) =>
      cols.map((column) => (column.id === columnId ? { ...column, cards: [...column.cards, card] } : column)),
    );
  }

  onCardAssigned(updatedCard: CardResponse): void {
    this.columns.update((cols) =>
      cols.map((column) => ({
        ...column,
        cards: column.cards.map((card) => (card.id === updatedCard.id ? updatedCard : card)),
      })),
    );
  }

  onDeleteCardRequested(cardId: number): void {
    this.deletingCardId.set(cardId);

    const dialogRef = this.dialog.open<boolean>(ConfirmModal);
    dialogRef.closed.subscribe((isConfirmed) => {
      if (isConfirmed) {
        this.deleteCard(cardId);
      } else {
        this.deletingCardId.set(null);
      }
    });
  }

  private deleteCard(cardId: number): void {
    this.deleteCardError.set(null);

    this.boardService
      .deleteCard(this.board().id, cardId)
      .pipe(finalize(() => this.deletingCardId.set(null)))
      .subscribe({
        next: () => {
          this.columns.update((cols) =>
            cols.map((column) => ({ ...column, cards: column.cards.filter((card) => card.id !== cardId) })),
          );
        },
        error: (err: HttpErrorResponse) =>
          this.deleteCardError.set(this.extractDeleteCardErrorMessage(err)),
      });
  }

  private extractDeleteCardErrorMessage(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error.length > 0) return err.error;
    return 'Could not delete card. Please try again.';
  }

  onCardDropped(event: CdkDragDrop<CardResponse[]>): void {
    const card = event.item.data as CardResponse;
    const expectedColumnId = this.findColumnIdForCards(event.previousContainer.data);
    const targetColumnId = this.findColumnIdForCards(event.container.data);
    if (expectedColumnId === null || targetColumnId === null) return;

    const expectedPosition = event.previousIndex + 1;
    const targetPosition = event.currentIndex + 1;
    const snapshot = this.columns();

    const sourceCards = [...event.previousContainer.data];
    const destinationCards = expectedColumnId === targetColumnId ? sourceCards : [...event.container.data];

    if (expectedColumnId === targetColumnId) {
      moveItemInArray(sourceCards, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(sourceCards, destinationCards, event.previousIndex, event.currentIndex);
    }

    this.columns.set(
      snapshot.map((column) => {
        if (column.id === expectedColumnId) return { ...column, cards: sourceCards };
        if (column.id === targetColumnId) return { ...column, cards: destinationCards };
        return column;
      }),
    );
    this.moveError.set(null);

    const touchedColumnIds = expectedColumnId === targetColumnId ? [expectedColumnId] : [expectedColumnId, targetColumnId];
    const generations = new Map(touchedColumnIds.map((id) => [id, this.bumpColumnGeneration(id)]));

    const request: MoveCardRequest = { targetColumnId, targetPosition, expectedColumnId, expectedPosition };
    const boardId = this.board().id;

    this.boardService.moveCard(boardId, card.id, request).subscribe({
      next: (response) => this.reconcile(response.affectedColumns, generations),
      error: () => this.handleMoveError(touchedColumnIds, generations, snapshot),
    });
  }

  private bumpColumnGeneration(columnId: number): number {
    const next = (this.columnGeneration.get(columnId) ?? 0) + 1;
    this.columnGeneration.set(columnId, next);
    return next;
  }

  private isCurrent(columnId: number, generation: number | undefined): boolean {
    return generation !== undefined && this.columnGeneration.get(columnId) === generation;
  }

  private findColumnIdForCards(cards: CardResponse[]): number | null {
    return this.columns().find((column) => column.cards === cards)?.id ?? null;
  }

  private reconcile(affectedColumns: readonly AffectedColumnResponse[], generations: Map<number, number>): void {
    const currentAffected = affectedColumns.filter((a) => this.isCurrent(a.columnId, generations.get(a.columnId)));
    if (currentAffected.length === 0) return;

    const cardsById = new Map<number, CardResponse>();
    for (const column of this.columns()) {
      for (const card of column.cards) cardsById.set(card.id, card);
    }

    let missingCard = false;
    const updated = this.columns().map((column) => {
      const affected = currentAffected.find((a) => a.columnId === column.id);
      if (!affected) return column;

      const cards: CardResponse[] = [];
      for (const { cardId, position } of affected.cards) {
        const card = cardsById.get(cardId);
        if (!card) {
          missingCard = true;
          break;
        }
        cards.push({ ...card, position });
      }
      return { ...column, cards };
    });

    if (missingCard) {
      this.refetchBoard();
      return;
    }

    this.columns.set(updated);
  }

  private handleMoveError(touchedColumnIds: number[], generations: Map<number, number>, snapshot: ColumnResponse[]): void {
    const currentColumnIds = touchedColumnIds.filter((id) => this.isCurrent(id, generations.get(id)));
    if (currentColumnIds.length === 0) return;

    this.columns.set(
      this.columns().map((column) => {
        if (!currentColumnIds.includes(column.id)) return column;
        return snapshot.find((original) => original.id === column.id) ?? column;
      }),
    );
    this.moveError.set('Could not move card — refreshing board.');
    this.refetchBoard();
  }

  private refetchBoard(): void {
    this.boardService.getById(this.board().id).subscribe((board) => {
      this.columns.set(this.cloneColumns(board.columns));
      this.members.set(board.members);
    });
  }

  private cloneColumns(columns: readonly ColumnResponse[]): ColumnResponse[] {
    return columns.map((column) => ({ ...column, cards: [...column.cards] }));
  }
}
