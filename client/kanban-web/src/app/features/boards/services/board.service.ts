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
  ColumnPositionResponse,
  ColumnResponse,
  CreateBoardRequest,
  CreateCardRequest,
  CreateColumnRequest,
  MoveCardRequest,
  MoveCardResponse,
  MoveColumnResponse,
  UpdateBoardRequest,
  UpdateColumnRequest,
} from '../models/board.models';
import { ColumnService } from './column.service';
import { BoardHubService } from './board-hub.service';

@Service()
export class BoardService {
  private api = inject(ApiService);
  private cardService = inject(CardService);
  private memberService = inject(MemberService);
  private columnService = inject(ColumnService);
  private hub = inject(BoardHubService);
  private columnGeneration = new Map<number, number>();
  private moveColumnGeneration = 0;

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
  boardDeleted = signal(false);
  isResyncing = signal(false);
  readonly connectionState = this.hub.connectionState;
  readonly isOffline = computed(() => this.connectionState() !== 'connected');

  constructor() {
    this.hub.cardCreated$.subscribe(({ columnId, card }) => this.addCardToColumn(columnId, card));
    this.hub.cardUpdated$.subscribe((card) => this.replaceCard(card));
    this.hub.cardAssigned$.subscribe((card) => this.replaceCard(card));
    this.hub.cardMoved$.subscribe((move) => this.applyCardMoved(move));
    this.hub.cardDeleted$.subscribe((cardId) => this.removeCard(cardId));
    this.hub.columnCreated$.subscribe((column) => this.addColumn(column));
    this.hub.columnUpdated$.subscribe((column) => this.updateColumnFields(column));
    this.hub.columnMoved$.subscribe((move) => this.applyColumnMoved(move));
    this.hub.columnDeleted$.subscribe((columnId) => this.removeColumn(columnId));
    this.hub.memberAdded$.subscribe((member) => this.appendMember(member));
    this.hub.boardUpdated$.subscribe((board) => this.applyBoardUpdated(board));
    this.hub.boardDeleted$.subscribe((boardId) => this.applyBoardDeleted(boardId));
    this.hub.reconnected$.subscribe(() => {
      if (this.boardId() !== null) this.refetchBoard();
    });
  }

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
    this.boardDeleted.set(false);
    this.isResyncing.set(false);
    this.columnGeneration.clear();
    this.moveColumnGeneration = 0;
    this.applyBoardSnapshot(board);
    this.hub.joinBoard$(board.id).subscribe();
  }

  leaveRealtimeBoard(): void {
    const boardId = this.boardId();
    if (boardId !== null) this.hub.leaveBoard$(boardId).subscribe();
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
      .pipe(tap((updated) => this.updateBoardFields(updated)));
  }

  createCard(request: CreateCardRequest): Observable<CardResponse> {
    const boardId = this.requireBoardId();
    return this.cardService.create(boardId, request).pipe(tap((card) => this.addCardToColumn(request.columnId, card)));
  }

  assignCard(cardId: number, request: AssignCardRequest): Observable<CardResponse> {
    const boardId = this.requireBoardId();
    this.assigningCardIds.update((ids) => new Set(ids).add(cardId));

    return this.cardService.assign(boardId, cardId, request).pipe(
      tap((updatedCard) => this.replaceCard(updatedCard)),
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
      tap(() => this.removeCard(cardId)),
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
    return this.memberService.add(boardId, request).pipe(tap((member) => this.appendMember(member)));
  }

  createColumn(request: CreateColumnRequest): Observable<ColumnResponse> {
    const boardId = this.requireBoardId();
    return this.columnService.create(boardId, request).pipe(tap((column) => this.addColumn(column)));
  }

  deleteColumn(columnId: number): Observable<void> {
    const boardId = this.requireBoardId();
    this.deletingColumnIds.update((ids) => new Set(ids).add(columnId));
    this.deleteColumnError.set(null);

    return this.columnService.delete(boardId, columnId).pipe(
      tap(() => this.removeColumn(columnId)),
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

  updateColumn(columnId: number, request: UpdateColumnRequest): Observable<ColumnResponse> {
    const boardId = this.requireBoardId();
    return this.columnService.update(boardId, columnId, request).pipe(tap((updated) => this.updateColumnFields(updated)));
  }

  moveColumn(event: CdkDragDrop<ColumnResponse[]>): void {
    const column = event.item.data as ColumnResponse;
    const expectedPosition = event.previousIndex + 1;
    const targetPosition = event.currentIndex + 1;
    const snapshot = this.columns();

    const reordered = [...snapshot];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.columns.set(reordered);
    this.moveError.set(null);

    const generation = ++this.moveColumnGeneration;
    const boardId = this.requireBoardId();

    this.columnService.move(boardId, column.id, { targetPosition, expectedPosition }).subscribe({
      next: (response) => this.reconcileColumns(response.affectedColumns, generation),
      error: () => this.handleMoveColumnError(generation, snapshot),
    });
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

  private reconcileColumns(affected: readonly ColumnPositionResponse[], generation: number): void {
    if (generation !== this.moveColumnGeneration) return;

    const positionById = new Map(affected.map((a) => [a.columnId, a.position]));
    this.columns.set(
      this.columns()
        .map((c) => (positionById.has(c.id) ? { ...c, position: positionById.get(c.id)! } : c))
        .sort((a, b) => a.position - b.position),
    );
  }

  private handleMoveColumnError(generation: number, snapshot: ColumnResponse[]): void {
    if (generation !== this.moveColumnGeneration) return;

    this.columns.set(snapshot);
    this.moveError.set('Could not move column — refreshing board.');
    this.refetchBoard();
  }

  private refetchBoard(): void {
    this.isResyncing.set(true);
    this.getById(this.requireBoardId())
      .pipe(finalize(() => this.isResyncing.set(false)))
      .subscribe((board) => this.applyBoardSnapshot(board));
  }

  private cloneColumns(columns: readonly ColumnResponse[]): ColumnResponse[] {
    return columns.map((column) => ({ ...column, cards: [...column.cards] }));
  }

  private addCardToColumn(columnId: number, card: CardResponse): void {
    this.columns.update((cols) =>
      cols.map((column) =>
        column.id === columnId && !column.cards.some((c) => c.id === card.id)
          ? { ...column, cards: [...column.cards, card] }
          : column,
      ),
    );
  }

  private replaceCard(card: CardResponse): void {
    this.columns.update((cols) =>
      cols.map((column) => ({
        ...column,
        cards: column.cards.map((c) => (c.id === card.id ? card : c)),
      })),
    );
  }

  private removeCard(cardId: number): void {
    this.columns.update((cols) => cols.map((column) => ({ ...column, cards: column.cards.filter((c) => c.id !== cardId) })));
  }

  private addColumn(column: ColumnResponse): void {
    this.columns.update((cols) => (cols.some((c) => c.id === column.id) ? cols : [...cols, column]));
  }

  private updateColumnFields(column: Pick<ColumnResponse, 'id' | 'title' | 'description'>): void {
    this.columns.update((cols) =>
      cols.map((c) => (c.id === column.id ? { ...c, title: column.title, description: column.description } : c)),
    );
  }

  private removeColumn(columnId: number): void {
    this.columns.update((cols) => cols.filter((c) => c.id !== columnId));
  }

  private appendMember(member: BoardMemberResponse): void {
    this.members.update((current) => (current.some((m) => m.memberId === member.memberId) ? current : [...current, member]));
  }

  private updateBoardFields(board: Pick<BoardResponse, 'name' | 'description'>): void {
    this.boardName.set(board.name);
    this.boardDescription.set(board.description);
  }

  private applyCardMoved(move: MoveCardResponse): void {
    const generations = new Map(move.affectedColumns.map((a) => [a.columnId, this.columnGeneration.get(a.columnId) ?? 0]));
    this.reconcile(move.affectedColumns, generations);
  }

  private applyColumnMoved(move: MoveColumnResponse): void {
    this.reconcileColumns(move.affectedColumns, this.moveColumnGeneration);
  }

  private applyBoardUpdated(board: BoardResponse): void {
    if (board.id !== this.boardId()) return;
    this.updateBoardFields(board);
  }

  private applyBoardDeleted(boardId: number): void {
    if (boardId !== this.boardId()) return;
    this.boardDeleted.set(true);
  }
}
