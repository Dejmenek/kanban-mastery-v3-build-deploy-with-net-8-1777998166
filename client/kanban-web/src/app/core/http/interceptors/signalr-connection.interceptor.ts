import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { BoardHubService } from '../../../features/boards/services/board-hub.service';

export const SIGNALR_CONNECTION_ID_HEADER = 'X-SignalR-Connection-Id';

export const signalRConnectionInterceptor: HttpInterceptorFn = (req, next) => {
  const boardHub = inject(BoardHubService);
  const connectionId = boardHub.getConnectionId();

  if (connectionId) {
    req = req.clone({ setHeaders: { [SIGNALR_CONNECTION_ID_HEADER]: connectionId } });
  }

  return next(req);
};
