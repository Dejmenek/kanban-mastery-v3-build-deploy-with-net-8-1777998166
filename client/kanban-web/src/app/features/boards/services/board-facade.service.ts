import { computed, inject, Service } from '@angular/core';
import { CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { catchError, EMPTY, finalize, Observable, tap } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { extractErrorMessage } from '../../../shared/utils/extract-error-message';
import {
  AddBoardMemberRequest,
  AssignCardRequest,
  BoardDetailsResponse,
  BoardMemberResponse,
  BoardResponse,
  CardResponse,
  ColumnResponse,
  CreateCardRequest,
  CreateColumnRequest,
  MoveCardRequest,
  UpdateBoardRequest,
  UpdateColumnRequest,
} from '../models/board.models';
import { BoardApiService } from './board-api.service';
import { BoardHubService } from './board-hub.service';
import { BoardStateService } from './board-state.service';
import { CardService } from './card.service';
import { ColumnService } from './column.service';
import { MemberService } from './member.service';

@Service()
export class BoardFacade {
  private state = inject(BoardStateService);
  private hub = inject(BoardHubService);
  private api = inject(BoardApiService);
  private cardService = inject(CardService);
  private memberService = inject(MemberService);
  private columnService = inject(ColumnService);

  readonly boardId = this.state.boardId;
  readonly boardName = this.state.boardName;
  readonly boardDescription = this.state.boardDescription;
  readonly userRole = this.state.userRole;
  readonly isOwner = this.state.isOwner;
  readonly columns = this.state.columns;
  readonly members = this.state.members;
  readonly deletingCardIds = this.state.deletingCardIds;
  readonly deletingColumnIds = this.state.deletingColumnIds;
  readonly assigningCardIds = this.state.assigningCardIds;
  readonly boardDeleted = this.state.boardDeleted;
  readonly moveError = this.state.moveError;
  readonly deleteCardError = this.state.deleteCardError;
  readonly deleteColumnError = this.state.deleteColumnError;
  readonly syncError = this.state.syncError;
  readonly isResyncing = this.state.isResyncing;
  readonly connectionState = this.hub.connectionState;
  readonly isOffline = computed(() => this.hub.connectionState().status !== 'connected');

  private columnGeneration = new Map<number, number>();
  private moveColumnGeneration = 0;

  constructor() {
    this.hub.cardCreated$.subscribe(({ columnId, card }) => {
      this.state.addCardToColumn(columnId, card);
    });
    this.hub.cardUpdated$.subscribe((card) => this.state.replaceCard(card));
    this.hub.cardAssigned$.subscribe((card) => this.state.replaceCard(card));
    this.hub.cardMoved$.subscribe((move) => {
      if (this.state.applyCardPositions(move.affectedColumns)) this.refetchBoard();
    });
    this.hub.cardDeleted$.subscribe((cardId) => this.state.removeCard(cardId));
    this.hub.columnCreated$.subscribe((column) => {
      this.state.addColumn(column);
    });
    this.hub.columnUpdated$.subscribe((column) => this.state.updateColumnFields(column));
    this.hub.columnMoved$.subscribe((move) => this.state.applyColumnPositions(move.affectedColumns));
    this.hub.columnDeleted$.subscribe((columnId) => this.state.removeColumn(columnId));
    this.hub.memberAdded$.subscribe((member) => this.state.appendMember(member));
    this.hub.boardUpdated$.subscribe((board) => this.state.applyBoardUpdated(board));
    this.hub.boardDeleted$.subscribe((boardId) => this.state.applyBoardDeleted(boardId));
    this.hub.reconnected$.subscribe(() => {
      if (this.state.boardId() !== null) this.refetchBoard();
    });
    this.hub.joinFailed$.subscribe((boardId) => {
      if (boardId === this.state.boardId()) {
        this.state.syncError.set("Live updates aren't working for this board. Try refreshing the page.");
      }
    });
    this.hub.joinSucceeded$.subscribe((boardId) => {
      if (boardId === this.state.boardId()) this.state.syncError.set(null);
    });
  }

  setBoard(board: BoardDetailsResponse): void {
    this.state.resetSession();
    this.columnGeneration.clear();
    this.moveColumnGeneration = 0;
    this.state.setBoard(board);
    this.state.applyBoardSnapshot(board);
    this.hub.joinBoard$(board.id).subscribe();
  }

  leaveRealtimeBoard(): void {
    const boardId = this.state.boardId();
    if (boardId !== null) this.hub.leaveBoard$(boardId).subscribe();
  }

  updateBoard(request: UpdateBoardRequest): Observable<BoardResponse> {
    const boardId = this.state.requireBoardId();
    return this.api.updateBoard(boardId, request).pipe(tap((updated) => this.state.updateBoardFields(updated)));
  }

  createCard(request: CreateCardRequest): Observable<CardResponse> {
    const boardId = this.state.requireBoardId();
    return this.cardService.create(boardId, request).pipe(tap((card) => this.state.addCardToColumn(request.columnId, card)));
  }

  assignCard(cardId: number, request: AssignCardRequest): Observable<CardResponse> {
    const boardId = this.state.requireBoardId();
    this.state.beginAssigningCard(cardId);

    return this.cardService.assign(boardId, cardId, request).pipe(
      tap((updatedCard) => this.state.replaceCard(updatedCard)),
      finalize(() => this.state.endAssigningCard(cardId)),
    );
  }

  deleteCard(cardId: number): Observable<void> {
    const boardId = this.state.requireBoardId();
    this.state.beginDeletingCard(cardId);
    this.state.deleteCardError.set(null);

    return this.cardService.delete(boardId, cardId).pipe(
      tap(() => this.state.removeCard(cardId)),
      catchError((err: HttpErrorResponse) => {
        this.state.deleteCardError.set(extractErrorMessage(err, 'Could not delete card. Please try again.'));
        return EMPTY;
      }),
      finalize(() => this.state.endDeletingCard(cardId)),
    );
  }

  addMember(request: AddBoardMemberRequest): Observable<BoardMemberResponse> {
    const boardId = this.state.requireBoardId();
    return this.memberService.add(boardId, request).pipe(tap((member) => this.state.appendMember(member)));
  }

  createColumn(request: CreateColumnRequest): Observable<ColumnResponse> {
    const boardId = this.state.requireBoardId();
    return this.columnService.create(boardId, request).pipe(tap((column) => this.state.addColumn(column)));
  }

  deleteColumn(columnId: number): Observable<void> {
    const boardId = this.state.requireBoardId();
    this.state.beginDeletingColumn(columnId);
    this.state.deleteColumnError.set(null);

    return this.columnService.delete(boardId, columnId).pipe(
      tap(() => this.state.removeColumn(columnId)),
      catchError((err: HttpErrorResponse) => {
        this.state.deleteColumnError.set(extractErrorMessage(err, 'Could not delete column. Please try again.'));
        return EMPTY;
      }),
      finalize(() => this.state.endDeletingColumn(columnId)),
    );
  }

  updateColumn(columnId: number, request: UpdateColumnRequest): Observable<ColumnResponse> {
    const boardId = this.state.requireBoardId();
    return this.columnService.update(boardId, columnId, request).pipe(tap((updated) => this.state.updateColumnFields(updated)));
  }

  moveColumn(event: CdkDragDrop<ColumnResponse[]>): void {
    const column = event.item.data as ColumnResponse;
    const expectedPosition = event.previousIndex + 1;
    const targetPosition = event.currentIndex + 1;
    const snapshot = this.state.columns();

    const reordered = [...snapshot];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.state.setColumns(reordered);
    this.state.moveError.set(null);

    const generation = ++this.moveColumnGeneration;
    const boardId = this.state.requireBoardId();

    this.columnService.move(boardId, column.id, { targetPosition, expectedPosition }).subscribe({
      next: (response) => {
        if (generation === this.moveColumnGeneration) this.state.applyColumnPositions(response.affectedColumns);
      },
      error: () => this.handleMoveColumnError(generation, snapshot),
    });
  }

  moveCard(event: CdkDragDrop<CardResponse[]>): void {
    const card = event.item.data as CardResponse;
    const expectedColumnId = this.state.findColumnIdForCards(event.previousContainer.data);
    const targetColumnId = this.state.findColumnIdForCards(event.container.data);
    if (expectedColumnId === null || targetColumnId === null) return;

    const expectedPosition = event.previousIndex + 1;
    const targetPosition = event.currentIndex + 1;
    const snapshot = this.state.columns();

    const sourceCards = [...event.previousContainer.data];
    const destinationCards = expectedColumnId === targetColumnId ? sourceCards : [...event.container.data];

    if (expectedColumnId === targetColumnId) {
      moveItemInArray(sourceCards, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(sourceCards, destinationCards, event.previousIndex, event.currentIndex);
    }

    this.state.setColumns(
      snapshot.map((column) => {
        if (column.id === expectedColumnId) return { ...column, cards: sourceCards };
        if (column.id === targetColumnId) return { ...column, cards: destinationCards };
        return column;
      }),
    );
    this.state.moveError.set(null);

    const touchedColumnIds = expectedColumnId === targetColumnId ? [expectedColumnId] : [expectedColumnId, targetColumnId];
    const generations = new Map(touchedColumnIds.map((id) => [id, this.bumpColumnGeneration(id)]));

    const request: MoveCardRequest = { targetColumnId, targetPosition, expectedColumnId, expectedPosition };
    const boardId = this.state.requireBoardId();

    this.cardService.move(boardId, card.id, request).subscribe({
      next: (response) => {
        const currentAffected = response.affectedColumns.filter((a) => this.isCurrent(a.columnId, generations.get(a.columnId)));
        if (currentAffected.length === 0) return;
        if (this.state.applyCardPositions(currentAffected)) this.refetchBoard();
      },
      error: () => this.handleMoveError(touchedColumnIds, generations, snapshot),
    });
  }

  private handleMoveError(touchedColumnIds: number[], generations: Map<number, number>, snapshot: ColumnResponse[]): void {
    const currentColumnIds = touchedColumnIds.filter((id) => this.isCurrent(id, generations.get(id)));
    if (currentColumnIds.length === 0) return;

    this.state.setColumns(
      this.state.columns().map((column) => {
        if (!currentColumnIds.includes(column.id)) return column;
        return snapshot.find((original) => original.id === column.id) ?? column;
      }),
    );
    this.state.moveError.set('Could not move card — refreshing board.');
    this.refetchBoard();
  }

  private handleMoveColumnError(generation: number, snapshot: ColumnResponse[]): void {
    if (generation !== this.moveColumnGeneration) return;

    this.state.setColumns(snapshot);
    this.state.moveError.set('Could not move column — refreshing board.');
    this.refetchBoard();
  }

  private refetchBoard(): void {
    this.state.isResyncing.set(true);
    this.api
      .getById(this.state.requireBoardId())
      .pipe(finalize(() => this.state.isResyncing.set(false)))
      .subscribe((board) => this.state.applyBoardSnapshot(board));
  }

  private bumpColumnGeneration(columnId: number): number {
    const next = (this.columnGeneration.get(columnId) ?? 0) + 1;
    this.columnGeneration.set(columnId, next);
    return next;
  }

  private isCurrent(columnId: number, generation: number | undefined): boolean {
    return generation !== undefined && this.columnGeneration.get(columnId) === generation;
  }
}
