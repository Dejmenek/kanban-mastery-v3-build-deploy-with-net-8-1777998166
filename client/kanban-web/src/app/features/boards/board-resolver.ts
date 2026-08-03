import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { RedirectCommand, ResolveFn, Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { redirectPathForStatus } from '../../core/http/utils/redirect-for-status';
import { BoardDetailsResponse } from './models/board.models';
import { BoardService } from './services/board.service';

export const boardResolver: ResolveFn<BoardDetailsResponse | RedirectCommand> = (route) => {
  const boardService = inject(BoardService);
  const router = inject(Router);
  const boardId = Number(route.paramMap.get('boardId'));

  if (!Number.isInteger(boardId)) {
    return new RedirectCommand(router.parseUrl('/not-found'));
  }

  return boardService.getById(boardId).pipe(
    catchError((error: HttpErrorResponse) =>
      of(new RedirectCommand(router.parseUrl(redirectPathForStatus(error.status)))),
    ),
  );
};
