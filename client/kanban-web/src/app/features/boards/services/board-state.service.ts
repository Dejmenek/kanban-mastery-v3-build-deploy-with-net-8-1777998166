import { computed, Service, signal } from '@angular/core';
import {
  AffectedColumnResponse,
  BoardDetailsResponse,
  BoardMemberResponse,
  BoardResponse,
  CardResponse,
  ColumnPositionResponse,
  ColumnResponse,
} from '../models/board.models';

@Service()
export class BoardStateService {
  private _boardId = signal<number | null>(null);
  private _boardName = signal('');
  private _boardDescription = signal<string | null>(null);
  private _columns = signal<ColumnResponse[]>([]);
  private _members = signal<BoardMemberResponse[]>([]);
  private _userRole = signal<string | null>(null);

  readonly boardId = this._boardId.asReadonly();
  readonly columns = this._columns.asReadonly();
  readonly userRole = this._userRole.asReadonly();
  readonly isOwner = computed(() => this._userRole() === 'Owner');
  readonly members = this._members.asReadonly();
  readonly boardName = this._boardName.asReadonly();
  readonly boardDescription = this._boardDescription.asReadonly();

  private _deletingCardIds = signal<ReadonlySet<number>>(new Set());
  private _deletingColumnIds = signal<ReadonlySet<number>>(new Set());
  private _assigningCardIds = signal<ReadonlySet<number>>(new Set());

  readonly deletingCardIds = this._deletingCardIds.asReadonly();
  readonly deletingColumnIds = this._deletingColumnIds.asReadonly();
  readonly assigningCardIds = this._assigningCardIds.asReadonly();

  beginDeletingCard(cardId: number): void {
    this._deletingCardIds.update((ids) => new Set(ids).add(cardId));
  }

  endDeletingCard(cardId: number): void {
    this._deletingCardIds.update((ids) => {
      const next = new Set(ids);
      next.delete(cardId);
      return next;
    });
  }

  beginDeletingColumn(columnId: number): void {
    this._deletingColumnIds.update((ids) => new Set(ids).add(columnId));
  }

  endDeletingColumn(columnId: number): void {
    this._deletingColumnIds.update((ids) => {
      const next = new Set(ids);
      next.delete(columnId);
      return next;
    });
  }

  beginAssigningCard(cardId: number): void {
    this._assigningCardIds.update((ids) => new Set(ids).add(cardId));
  }

  endAssigningCard(cardId: number): void {
    this._assigningCardIds.update((ids) => {
      const next = new Set(ids);
      next.delete(cardId);
      return next;
    });
  }

  readonly boardDeleted = signal(false);
  readonly moveError = signal<string | null>(null);
  readonly deleteCardError = signal<string | null>(null);
  readonly deleteColumnError = signal<string | null>(null);
  readonly syncError = signal<string | null>(null);
  readonly isResyncing = signal(false);

  requireBoardId(): number {
    return this.boardId()!;
  }

  setBoard(board: BoardDetailsResponse): void {
    this._boardId.set(board.id);
    this._boardName.set(board.name);
    this._boardDescription.set(board.description);
    this._userRole.set(board.userRole);
  }

  applyBoardSnapshot(board: { columns: readonly ColumnResponse[]; members: BoardMemberResponse[] }): void {
    this._columns.set(this.cloneColumns(board.columns));
    this._members.set(board.members);
  }

  resetSession(): void {
    this.moveError.set(null);
    this.deleteCardError.set(null);
    this.deleteColumnError.set(null);
    this.syncError.set(null);
    this._deletingCardIds.set(new Set());
    this._deletingColumnIds.set(new Set());
    this._assigningCardIds.set(new Set());
    this.boardDeleted.set(false);
    this.isResyncing.set(false);
  }

  cloneColumns(columns: readonly ColumnResponse[]): ColumnResponse[] {
    return columns.map((column) => ({ ...column, cards: [...column.cards] }));
  }

  findColumnIdForCards(cards: CardResponse[]): number | null {
    return this.columns().find((column) => column.cards === cards)?.id ?? null;
  }

  setColumns(columns: ColumnResponse[]): void {
    this._columns.set(columns);
  }

  applyColumnPositions(affected: readonly ColumnPositionResponse[]): void {
    const positionById = new Map(affected.map((a) => [a.columnId, a.position]));
    this._columns.set(
      this.columns()
        .map((c) => (positionById.has(c.id) ? { ...c, position: positionById.get(c.id)! } : c))
        .sort((a, b) => a.position - b.position),
    );
  }

  /** Applies affected columns' card positions. Returns true if a referenced card wasn't found locally (caller should resync). */
  applyCardPositions(affectedColumns: readonly AffectedColumnResponse[]): boolean {
    const cardsById = new Map<number, CardResponse>();
    for (const column of this.columns()) {
      for (const card of column.cards) cardsById.set(card.id, card);
    }

    let missingCard = false;
    const updated = this.columns().map((column) => {
      const affected = affectedColumns.find((a) => a.columnId === column.id);
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

    if (missingCard) return true;

    this._columns.set(updated);
    return false;
  }

  addCardToColumn(columnId: number, card: CardResponse): void {
    this._columns.update((cols) =>
      cols.map((column) =>
        column.id === columnId && !column.cards.some((c) => c.id === card.id)
          ? { ...column, cards: [...column.cards, card] }
          : column,
      ),
    );
  }

  replaceCard(card: CardResponse): void {
    this._columns.update((cols) =>
      cols.map((column) => ({
        ...column,
        cards: column.cards.map((c) => (c.id === card.id ? card : c)),
      })),
    );
  }

  removeCard(cardId: number): void {
    this._columns.update((cols) => cols.map((column) => ({ ...column, cards: column.cards.filter((c) => c.id !== cardId) })));
  }

  addColumn(column: ColumnResponse): void {
    this._columns.update((cols) => (cols.some((c) => c.id === column.id) ? cols : [...cols, column]));
  }

  updateColumnFields(column: Pick<ColumnResponse, 'id' | 'title' | 'description'>): void {
    this._columns.update((cols) =>
      cols.map((c) => (c.id === column.id ? { ...c, title: column.title, description: column.description } : c)),
    );
  }

  removeColumn(columnId: number): void {
    this._columns.update((cols) => cols.filter((c) => c.id !== columnId));
  }

  appendMember(member: BoardMemberResponse): void {
    this._members.update((current) => (current.some((m) => m.memberId === member.memberId) ? current : [...current, member]));
  }

  updateBoardFields(board: Pick<BoardResponse, 'name' | 'description'>): void {
    this._boardName.set(board.name);
    this._boardDescription.set(board.description);
  }

  applyBoardUpdated(board: BoardResponse): void {
    if (board.id !== this.boardId()) return;
    this.updateBoardFields(board);
  }

  applyBoardDeleted(boardId: number): void {
    if (boardId !== this.boardId()) return;
    this.boardDeleted.set(true);
  }
}
