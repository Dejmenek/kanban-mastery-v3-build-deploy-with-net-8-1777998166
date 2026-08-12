import { computed, inject, signal, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { CardService } from './card.service';
import { MemberService } from './member.service';
import { catchError, finalize, Observable, tap, EMPTY } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { extractErrorMessage } from '../../../shared/utils/extract-error-message';
import {
  AddBoardMemberRequest,
  AffectedColumnResponse,
  AssignCardRequest,
  BoardDetailsResponse,
  BoardMemberResponse,
  BoardResponse,
  BoardSummaryResponse,
  CardResponse,
  ColumnResponse,
  CreateBoardRequest,
  CreateCardRequest,
  CreateColumnRequest,
  MoveCardRequest,
  UpdateBoardRequest,
} from '../models/board.models';
import { ColumnService } from './column.service';

@Service()
export class BoardService {
  private api = inject(ApiService);
  private cardService = inject(CardService);
  private memberService = inject(MemberService);
  private columnService = inject(ColumnService);
  private columnGeneration = new Map<number, number>();

  boardId = signal<number | null>(null);
  boardName = signal('');
  boardDescription = signal<string | null>(null);
  userRole = signal<string | null>(null);
  isOwner = computed(() => this.userRole() === 'Owner');
  columns = signal<ColumnResponse[]>([]);
  members = signal<BoardMemberResponse[]>([]);
  moveError = signal<string | null>(null);
  deleteCardError = signal<string | null>(null);
  deleteColumnError = signal<string | null>(null);
  deletingCardIds = signal<ReadonlySet<number>>(new Set());
  deletingColumnIds = signal<ReadonlySet<number>>(new Set());
  assigningCardIds = signal<ReadonlySet<number>>(new Set());

  setBoard(board: BoardDetailsResponse): void {
    this.boardId.set(board.id);
    this.boardName.set(board.name);
    this.boardDescription.set(board.description);
    this.userRole.set(board.userRole);
    this.moveError.set(null);
    this.deleteCardError.set(null);
    this.deleteColumnError.set(null);
    this.deletingCardIds.set(new Set());
    this.deletingColumnIds.set(new Set());
    this.assigningCardIds.set(new Set());
    this.columnGeneration.clear();
    this.applyBoardSnapshot(board);
  }

  getAll(): Observable<BoardSummaryResponse[]> {
    return this.api
      .get<BoardSummaryResponse[]>('/api/boards');
  }

  getById(boardId: number): Observable<BoardDetailsResponse> {
    return this.api
      .get<BoardDetailsResponse>(`/api/boards/${boardId}`);
  }

  createBoard(request: CreateBoardRequest): Observable<BoardSummaryResponse> {
    return this.api
      .post<CreateBoardRequest, BoardSummaryResponse>(`/api/boards`, request);
  }

  deleteBoard(boardId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/boards/${boardId}`);
  }

  updateBoard(request: UpdateBoardRequest): Observable<BoardResponse> {
    const boardId = this.requireBoardId();
    return this.api
      .put<UpdateBoardRequest, BoardResponse>(`/api/boards/${boardId}`, request)
      .pipe(
        tap((updated) => {
          this.boardName.set(updated.name);
          this.boardDescription.set(updated.description);
        }),
      );
  }

  createCard(request: CreateCardRequest): Observable<CardResponse> {
    const boardId = this.requireBoardId();
    return this.cardService.create(boardId, request).pipe(
      tap((card) =>
        this.columns.update((cols) =>
          cols.map((column) => (column.id === request.columnId ? { ...column, cards: [...column.cards, card] } : column)),
        ),
      ),
    );
  }

  assignCard(cardId: number, request: AssignCardRequest): Observable<CardResponse> {
    const boardId = this.requireBoardId();
    this.assigningCardIds.update((ids) => new Set(ids).add(cardId));

    return this.cardService.assign(boardId, cardId, request).pipe(
      tap((updatedCard) =>
        this.columns.update((cols) =>
          cols.map((column) => ({
            ...column,
            cards: column.cards.map((card) => (card.id === updatedCard.id ? updatedCard : card)),
          })),
        ),
      ),
      finalize(() =>
        this.assigningCardIds.update((ids) => {
          const next = new Set(ids);
          next.delete(cardId);
          return next;
        }),
      ),
    );
  }

  deleteCard(cardId: number): Observable<void> {
    const boardId = this.requireBoardId();
    this.deletingCardIds.update((ids) => new Set(ids).add(cardId));
    this.deleteCardError.set(null);

    return this.cardService.delete(boardId, cardId).pipe(
      tap(() =>
        this.columns.update((cols) =>
          cols.map((column) => ({ ...column, cards: column.cards.filter((card) => card.id !== cardId) })),
        ),
      ),
      catchError((err: HttpErrorResponse) => {
        this.deleteCardError.set(extractErrorMessage(err, 'Could not delete card. Please try again.'));
        return EMPTY;
      }),
      finalize(() =>
        this.deletingCardIds.update((ids) => {
          const next = new Set(ids);
          next.delete(cardId);
          return next;
        }),
      ),
    );
  }

  addMember(request: AddBoardMemberRequest): Observable<BoardMemberResponse> {
    const boardId = this.requireBoardId();
    return this.memberService.add(boardId, request).pipe(
      tap((member) => this.members.update((current) => [...current, member])),
    );
  }

  createColumn(request: CreateColumnRequest): Observable<ColumnResponse> {
    const boardId = this.requireBoardId();
    return this.columnService.create(boardId, request).pipe(
      tap((column) => this.columns.update((current) => [...current, column])),
    );
  }

  deleteColumn(columnId: number): Observable<void> {
    const boardId = this.requireBoardId();
    this.deletingColumnIds.update((ids) => new Set(ids).add(columnId));
    this.deleteColumnError.set(null);

    return this.columnService.delete(boardId, columnId).pipe(
      tap(() => this.columns.update((current) => current.filter((column) => column.id !== columnId))),
      catchError((err: HttpErrorResponse) => {
        this.deleteColumnError.set(extractErrorMessage(err, 'Could not delete column. Please try again.'));
        return EMPTY;
      }),
      finalize(() =>
        this.deletingColumnIds.update((ids) => {
          const next = new Set(ids);
          next.delete(columnId);
          return next;
        }),
      ),
    );
  }

  moveCard(event: CdkDragDrop<CardResponse[]>): void {
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
    const boardId = this.requireBoardId();

    this.cardService.move(boardId, card.id, request).subscribe({
      next: (response) => this.reconcile(response.affectedColumns, generations),
      error: () => this.handleMoveError(touchedColumnIds, generations, snapshot),
    });
  }

  private requireBoardId(): number {
    return this.boardId()!;
  }

  private applyBoardSnapshot(board: { columns: readonly ColumnResponse[]; members: BoardMemberResponse[] }): void {
    this.columns.set(this.cloneColumns(board.columns));
    this.members.set(board.members);
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
    this.getById(this.requireBoardId()).subscribe((board) => this.applyBoardSnapshot(board));
  }

  private cloneColumns(columns: readonly ColumnResponse[]): ColumnResponse[] {
    return columns.map((column) => ({ ...column, cards: [...column.cards] }));
  }
}
