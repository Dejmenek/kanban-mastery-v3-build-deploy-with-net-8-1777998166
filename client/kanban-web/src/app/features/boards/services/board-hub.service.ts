import { Service, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import * as signalR from '@microsoft/signalr';
import {
  EMPTY,
  NEVER,
  Observable,
  catchError,
  concat,
  defer,
  distinctUntilChanged,
  filter,
  finalize,
  from,
  fromEventPattern,
  map,
  merge,
  of,
  shareReplay,
  switchMap,
  take,
  timer,
} from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/services/auth.service';
import { TokenService } from '../../../core/auth/services/token.service';
import { BoardMemberResponse, BoardResponse, CardResponse, ColumnResponse, MoveCardResponse, MoveColumnResponse } from '../models/board.models';

export type HubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

const CONNECTION_STATUS_DISPLAY_DELAY_MS = 400;

export interface CardCreatedEvent {
  columnId: number;
  card: CardResponse;
}

function connectHub$(
  url: string,
  accessTokenFactory: () => string,
  registerHandlers: (connection: signalR.HubConnection) => void,
): Observable<signalR.HubConnection> {
  return defer(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory, withCredentials: false })
      .withAutomaticReconnect()
      .build();

    registerHandlers(connection);

function hubEvent$<T extends unknown[]>(connection: signalR.HubConnection, methodName: string): Observable<T> {
  return fromEventPattern<T>(
    (handler) => connection.on(methodName, handler),
    (handler) => connection.off(methodName, handler),
    (...args: unknown[]) => args as T,
  );
}

function hubLifecycle$(connection: signalR.HubConnection): Observable<HubConnectionState> {
  return merge(
    fromEventPattern<void>((handler) => connection.onreconnecting(handler)).pipe(map(() => 'reconnecting' as const)),
    fromEventPattern<void>((handler) => connection.onreconnected(handler)).pipe(map(() => 'connected' as const)),
    fromEventPattern<void>((handler) => connection.onclose(handler)).pipe(map(() => 'disconnected' as const)),
  );
}

@Service()
export class BoardHubService {
  private tokenService = inject(TokenService);
  private authService = inject(AuthService);

  private currentBoardId: number | null = null;
  private wantConnection = signal(false);

  private connection$: Observable<signalR.HubConnection | null> = toObservable(
    computed(() => this.authService.loggedIn() && this.wantConnection()),
  ).pipe(
    distinctUntilChanged(),
    switchMap((shouldConnect) =>
      shouldConnect
        ? connectHub$(`${environment.apiUrl}/hubs/board`, () => this.tokenService.getToken() ?? '', (connection) =>
            this.registerHandlers(connection),
          ).pipe(catchError(() => EMPTY))
        : of(null),
    ),
    shareReplay(1),
  );

  readonly connectionState = toSignal(
    this.connection$.pipe(
      switchMap((connection) =>
        connection ? merge(of<HubConnectionState>('connected'), hubLifecycle$(connection)) : of<HubConnectionState>('disconnected'),
      ),
      switchMap((state) => (state === 'connected' ? of(state) : timer(CONNECTION_STATUS_DISPLAY_DELAY_MS).pipe(map(() => state)))),
    ),
    { initialValue: 'connected' as HubConnectionState },
  );

  private readonly currentConnection = toSignal(this.connection$, { initialValue: null as signalR.HubConnection | null });

  getConnectionId(): string | null {
    return this.currentConnection()?.connectionId ?? null;
  }

  readonly cardCreated$ = this.event$<[columnId: number, card: CardResponse]>('CardCreated').pipe(
    map(([columnId, card]): CardCreatedEvent => ({ columnId, card })),
  );
  readonly cardUpdated$ = this.event$<[card: CardResponse]>('CardUpdated').pipe(map(([card]) => card));
  readonly cardAssigned$ = this.event$<[card: CardResponse]>('CardAssigned').pipe(map(([card]) => card));
  readonly cardMoved$ = this.event$<[moveCard: MoveCardResponse]>('CardMoved').pipe(map(([moveCard]) => moveCard));
  readonly cardDeleted$ = this.event$<[cardId: number]>('CardDeleted').pipe(map(([cardId]) => cardId));
  readonly columnCreated$ = this.event$<[column: ColumnResponse]>('ColumnCreated').pipe(map(([column]) => column));
  readonly columnUpdated$ = this.event$<[column: ColumnResponse]>('ColumnUpdated').pipe(map(([column]) => column));
  readonly columnMoved$ = this.event$<[moveColumn: MoveColumnResponse]>('ColumnMoved').pipe(map(([moveColumn]) => moveColumn));
  readonly columnDeleted$ = this.event$<[columnId: number]>('ColumnDeleted').pipe(map(([columnId]) => columnId));
  readonly memberAdded$ = this.event$<[member: BoardMemberResponse]>('MemberAdded').pipe(map(([member]) => member));
  readonly boardUpdated$ = this.event$<[board: BoardResponse]>('BoardUpdated').pipe(map(([board]) => board));
  readonly boardDeleted$ = this.event$<[boardId: number]>('BoardDeleted').pipe(map(([boardId]) => boardId));

  readonly reconnected$: Observable<void> = this.connection$.pipe(
    switchMap((connection) => (connection ? hubLifecycle$(connection) : EMPTY)),
    filter((state) => state === 'connected'),
    map(() => undefined),
  );

  constructor() {
    effect(() => {
      if (!this.authService.loggedIn()) this.wantConnection.set(false);
    });

    this.connection$
      .pipe(switchMap((connection) => (connection ? hubLifecycle$(connection) : EMPTY)))
      .subscribe((state) => {
        if (state === 'connected' && this.currentBoardId !== null) {
          this.invoke$('JoinBoard', this.currentBoardId).subscribe();
        }
      });
  }

  joinBoard$(boardId: number): Observable<void> {
    this.wantConnection.set(true);

    const previousBoardId = this.currentBoardId;
    this.currentBoardId = boardId;

    const leavePrevious$ =
      previousBoardId !== null && previousBoardId !== boardId ? this.invoke$('LeaveBoard', previousBoardId) : of(undefined);
    return leavePrevious$.pipe(switchMap(() => this.invoke$('JoinBoard', boardId)));
  }

  leaveBoard$(boardId: number): Observable<void> {
    if (this.currentBoardId === boardId) this.currentBoardId = null;
    return this.invoke$('LeaveBoard', boardId);
  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('CardCreated', (columnId: number, card: CardResponse) => this.cardCreatedSubject.next([columnId, card]));
    connection.on('CardUpdated', (card: CardResponse) => this.cardUpdatedSubject.next([card]));
    connection.on('CardAssigned', (card: CardResponse) => this.cardAssignedSubject.next([card]));
    connection.on('CardMoved', (moveCard: MoveCardResponse) => this.cardMovedSubject.next([moveCard]));
    connection.on('CardDeleted', (cardId: number) => this.cardDeletedSubject.next([cardId]));
    connection.on('ColumnCreated', (column: ColumnResponse) => this.columnCreatedSubject.next([column]));
    connection.on('ColumnUpdated', (column: ColumnResponse) => this.columnUpdatedSubject.next([column]));
    connection.on('ColumnMoved', (moveColumn: MoveColumnResponse) => this.columnMovedSubject.next([moveColumn]));
    connection.on('ColumnDeleted', (columnId: number) => this.columnDeletedSubject.next([columnId]));
    connection.on('MemberAdded', (member: BoardMemberResponse) => this.memberAddedSubject.next([member]));
    connection.on('BoardUpdated', (board: BoardResponse) => this.boardUpdatedSubject.next([board]));
    connection.on('BoardDeleted', (boardId: number) => this.boardDeletedSubject.next([boardId]));
  }

  private event$<T extends unknown[]>(methodName: string): Observable<T> {
    return this.connection$.pipe(switchMap((connection) => (connection ? hubEvent$<T>(connection, methodName) : EMPTY)));
  }

  private invoke$(method: 'JoinBoard' | 'LeaveBoard', boardId: number): Observable<void> {
    return this.connection$.pipe(
      take(1),
      switchMap((connection) => (connection?.state === signalR.HubConnectionState.Connected ? from(connection.invoke(method, boardId)) : EMPTY)),
      catchError(() => EMPTY),
    );
  }
}
