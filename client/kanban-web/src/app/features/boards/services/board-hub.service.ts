import { Service, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import * as signalR from '@microsoft/signalr';
import {
  EMPTY,
  Observable,
  Subject,
  catchError,
  combineLatest,
  defer,
  distinctUntilChanged,
  filter,
  finalize,
  from,
  fromEventPattern,
  map,
  merge,
  of,
  retry,
  shareReplay,
  switchMap,
  take,
  throwError,
  timer,
} from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/services/auth.service';
import { TokenService } from '../../../core/auth/services/token.service';
import { BoardMemberResponse, BoardResponse, CardResponse, ColumnResponse, MoveCardResponse, MoveColumnResponse } from '../models/board.models';

export type HubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

const CONNECTION_STATUS_DISPLAY_DELAY_MS = 400;
const RECONNECT_BASE_DELAY_MS = 1000;
const RECONNECT_MAX_DELAY_MS = 30000;
const INVOKE_RETRY_COUNT = 3;
const INVOKE_RETRY_BASE_DELAY_MS = 500;
const INVOKE_RETRY_MAX_DELAY_MS = 4000;

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

    const closed$ = fromEventPattern<Error | undefined>((handler) => connection.onclose(handler)).pipe(
      switchMap((error) => throwError(() => error ?? new Error('SignalR connection was closed.'))),
    );

    return from(connection.start()).pipe(
      switchMap(() => merge(of(connection), closed$)),
      finalize(() => {
        void connection.stop();
      }),
    );
  }).pipe(
    retry({
      delay: (attempt) => {
        const delay = Math.min(RECONNECT_BASE_DELAY_MS * 2 ** (attempt - 1), RECONNECT_MAX_DELAY_MS);
        return timer(delay);
      },
      resetOnSuccess: true,
    }),
  );
}

function invokeWithRetry$(connection: signalR.HubConnection, method: 'JoinBoard' | 'LeaveBoard', boardId: number): Observable<void> {
  return from(connection.invoke(method, boardId)).pipe(
    retry({
      count: INVOKE_RETRY_COUNT,
      delay: (attempt) => {
        const delay = Math.min(INVOKE_RETRY_BASE_DELAY_MS * 2 ** (attempt - 1), INVOKE_RETRY_MAX_DELAY_MS);
        return timer(delay);
      },
    }),
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

  private currentBoardId = signal<number | null>(null);
  private joinedBoardId: number | null = null;
  private wantConnection = signal(false);

  private readonly cardCreatedSubject = new Subject<[columnId: number, card: CardResponse]>();
  private readonly cardUpdatedSubject = new Subject<[card: CardResponse]>();
  private readonly cardAssignedSubject = new Subject<[card: CardResponse]>();
  private readonly cardMovedSubject = new Subject<[moveCard: MoveCardResponse]>();
  private readonly cardDeletedSubject = new Subject<[cardId: number]>();
  private readonly columnCreatedSubject = new Subject<[column: ColumnResponse]>();
  private readonly columnUpdatedSubject = new Subject<[column: ColumnResponse]>();
  private readonly columnMovedSubject = new Subject<[moveColumn: MoveColumnResponse]>();
  private readonly columnDeletedSubject = new Subject<[columnId: number]>();
  private readonly memberAddedSubject = new Subject<[member: BoardMemberResponse]>();
  private readonly boardUpdatedSubject = new Subject<[board: BoardResponse]>();
  private readonly boardDeletedSubject = new Subject<[boardId: number]>();

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

  private readonly liveConnection$: Observable<signalR.HubConnection> = this.connection$.pipe(
    switchMap((connection) =>
      connection
        ? merge(
            of(connection),
            hubLifecycle$(connection).pipe(
              filter((state) => state === 'connected'),
              map(() => connection),
            ),
          )
        : EMPTY,
    ),
  );

  readonly connectionState = toSignal(
    this.connection$.pipe(
      switchMap((connection) =>
        connection ? merge(of<HubConnectionState>('connected'), hubLifecycle$(connection)) : of<HubConnectionState>('disconnected'),
      ),
      switchMap((state) => (state === 'connected' ? of(state) : timer(CONNECTION_STATUS_DISPLAY_DELAY_MS).pipe(map(() => state)))),
    ),
    { initialValue: 'disconnected' as HubConnectionState },
  );

  private readonly currentConnection = toSignal(this.connection$, { initialValue: null as signalR.HubConnection | null });

  getConnectionId(): string | null {
    return this.currentConnection()?.connectionId ?? null;
  }

  readonly cardCreated$ = this.cardCreatedSubject.pipe(map(([columnId, card]): CardCreatedEvent => ({ columnId, card })));
  readonly cardUpdated$ = this.cardUpdatedSubject.pipe(map(([card]) => card));
  readonly cardAssigned$ = this.cardAssignedSubject.pipe(map(([card]) => card));
  readonly cardMoved$ = this.cardMovedSubject.pipe(map(([moveCard]) => moveCard));
  readonly cardDeleted$ = this.cardDeletedSubject.pipe(map(([cardId]) => cardId));
  readonly columnCreated$ = this.columnCreatedSubject.pipe(map(([column]) => column));
  readonly columnUpdated$ = this.columnUpdatedSubject.pipe(map(([column]) => column));
  readonly columnMoved$ = this.columnMovedSubject.pipe(map(([moveColumn]) => moveColumn));
  readonly columnDeleted$ = this.columnDeletedSubject.pipe(map(([columnId]) => columnId));
  readonly memberAdded$ = this.memberAddedSubject.pipe(map(([member]) => member));
  readonly boardUpdated$ = this.boardUpdatedSubject.pipe(map(([board]) => board));
  readonly boardDeleted$ = this.boardDeletedSubject.pipe(map(([boardId]) => boardId));

  private readonly rejoinedSubject = new Subject<void>();
  readonly reconnected$: Observable<void> = this.rejoinedSubject.asObservable();

  private readonly joinFailedSubject = new Subject<number>();
  readonly joinFailed$: Observable<number> = this.joinFailedSubject.asObservable();

  private readonly joinSucceededSubject = new Subject<number>();
  readonly joinSucceeded$: Observable<number> = this.joinSucceededSubject.asObservable();

  constructor() {
    effect(() => {
      if (!this.authService.loggedIn()) this.wantConnection.set(false);
    });

    combineLatest([this.liveConnection$, toObservable(this.currentBoardId)])
      .pipe(
        switchMap(([connection, boardId]) =>
          boardId === null
            ? EMPTY
            : invokeWithRetry$(connection, 'JoinBoard', boardId).pipe(
                map(() => boardId),
                catchError(() => {
                  this.joinFailedSubject.next(boardId);
                  return EMPTY;
                }),
              ),
        ),
      )
      .subscribe((boardId) => {
        const rejoined = this.joinedBoardId === boardId;
        this.joinedBoardId = boardId;
        this.joinSucceededSubject.next(boardId);
        if (rejoined) this.rejoinedSubject.next();
      });
  }

  joinBoard$(boardId: number): Observable<void> {
    this.wantConnection.set(true);

    const previousBoardId = this.currentBoardId();
    if (previousBoardId === boardId) {
      return of(undefined);
    }

    this.joinedBoardId = null;
    this.currentBoardId.set(boardId);

    return previousBoardId !== null ? this.invokeIfConnected$('LeaveBoard', previousBoardId) : of(undefined);
  }

  leaveBoard$(boardId: number): Observable<void> {
    if (this.currentBoardId() === boardId) {
      this.currentBoardId.set(null);
      this.joinedBoardId = null;
    }
    return this.invokeIfConnected$('LeaveBoard', boardId);
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('CardCreated', (columnId: number, card: CardResponse) => {
      this.cardCreatedSubject.next([columnId, card]);
    });
    connection.on('CardUpdated', (card: CardResponse) => {
      this.cardUpdatedSubject.next([card]);
    });
    connection.on('CardAssigned', (card: CardResponse) => {
      this.cardAssignedSubject.next([card]);
    });
    connection.on('CardMoved', (moveCard: MoveCardResponse) => {
      this.cardMovedSubject.next([moveCard]);
    });
    connection.on('CardDeleted', (cardId: number) => {
      this.cardDeletedSubject.next([cardId]);
    });
    connection.on('ColumnCreated', (column: ColumnResponse) => {
      this.columnCreatedSubject.next([column]);
    });
    connection.on('ColumnUpdated', (column: ColumnResponse) => {
      this.columnUpdatedSubject.next([column]);
    });
    connection.on('ColumnMoved', (moveColumn: MoveColumnResponse) => {
      this.columnMovedSubject.next([moveColumn]);
    });
    connection.on('ColumnDeleted', (columnId: number) => {
      this.columnDeletedSubject.next([columnId]);
    });
    connection.on('MemberAdded', (member: BoardMemberResponse) => {
      this.memberAddedSubject.next([member]);
    });
    connection.on('BoardUpdated', (board: BoardResponse) => {
      this.boardUpdatedSubject.next([board]);
    });
    connection.on('BoardDeleted', (boardId: number) => {
      this.boardDeletedSubject.next([boardId]);
    });
  }

  private invokeIfConnected$(method: 'JoinBoard' | 'LeaveBoard', boardId: number): Observable<void> {
    return this.connection$.pipe(
      take(1),
      switchMap((connection) => {
        if (connection?.state !== signalR.HubConnectionState.Connected) {
          return EMPTY;
        }
        return invokeWithRetry$(connection, method, boardId);
      }),
      catchError(() => EMPTY),
    );
  }
}
