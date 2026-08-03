import { Routes } from '@angular/router';
import { authGuard } from './core/auth/guards/auth-guard';
import { boardResolver } from './features/boards/board-resolver';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/pages/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/pages/register/register').then((m) => m.Register),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'board/:boardId',
        canActivate: [authGuard],
        resolve: { board: boardResolver },
        loadComponent: () => import('./features/boards/pages/board-detail/board-detail').then((m) => m.BoardDetail),
  },
  {
        path: 'forbidden',
        loadComponent: () => import('./shared/pages/forbidden/forbidden').then((m) => m.Forbidden),
      },
      {
        path: 'error',
        loadComponent: () => import('./shared/pages/error-page/error-page').then((m) => m.ErrorPage),
  },
  {
        path: 'not-found',
        loadComponent: () => import('./shared/pages/not-found/not-found').then((m) => m.NotFound),
      },
      { path: '**', redirectTo: 'not-found' },
    ],
  },
];
