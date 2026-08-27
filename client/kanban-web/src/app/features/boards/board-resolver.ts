import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { RedirectCommand, ResolveFn, Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { redirectPathForStatus } from '../../core/http/utils/redirect-for-status';
import { BoardDetailsResponse } from './models/board.models';
import { BoardApiService } from './services/board-api.service';

export const boardResolver: ResolveFn<BoardDetailsResponse | RedirectCommand> = (route) => {
  const boardApiService = inject(BoardApiService);
  const router = inject(Router);
  const boardId = Number(route.paramMap.get('boardId'));

  if (!Number.isInteger(boardId)) {
    return new RedirectCommand(router.parseUrl('/not-found'));
  }

  return boardApiService.getById(boardId).pipe(
    catchError((error: HttpErrorResponse) =>
      of(new RedirectCommand(router.parseUrl(redirectPathForStatus(error.status)))),
    ),
  );
};
