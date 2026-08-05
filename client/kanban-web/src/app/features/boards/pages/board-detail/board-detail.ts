import { Component, inject, input, linkedSignal, signal } from '@angular/core';
import { BoardColumn } from '../../components/board-column/board-column';
import {
  AffectedColumnResponse,
  BoardDetailsResponse,
  CardResponse,
  ColumnResponse,
  MoveCardRequest,
} from '../../models/board.models';
import { BoardService } from '../../services/board.service';
import { CdkDragDrop, CdkDropListGroup, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-board-detail',
  templateUrl: './board-detail.html',
  styleUrl: './board-detail.css',
  imports: [BoardColumn, CdkDropListGroup],
})
export class BoardDetail {
  board = input.required<BoardDetailsResponse>();

  private boardService = inject(BoardService);
  private columnGeneration = new Map<number, number>();

  protected columns = linkedSignal(() => this.cloneColumns(this.board().columns));
  protected moveError = signal<string | null>(null);

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
    });
  }

  private cloneColumns(columns: readonly ColumnResponse[]): ColumnResponse[] {
    return columns.map((column) => ({ ...column, cards: [...column.cards] }));
  }
}
