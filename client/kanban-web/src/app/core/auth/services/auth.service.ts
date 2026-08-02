import { Service, inject, signal } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { ApiService } from '../../http/services/api.service';
import { LoginRequest, LoginResponse, RegisterRequest } from '../models/auth.models';
import { TokenService } from './token.service';

@Service()
export class AuthService {
  private api = inject(ApiService);
  private tokenService = inject(TokenService);

  private _loggedIn = signal<boolean>(this.tokenService.isTokenValid());
  readonly loggedIn = this._loggedIn.asReadonly();

  isAuthenticated(): boolean {
    const valid = this.tokenService.isTokenValid();
    this._loggedIn.set(valid);
    return valid;
  }

  login(credentials: LoginRequest): Observable<void> {
    return this.api
      .post<LoginRequest, LoginResponse>('/login', credentials)
      .pipe(map((response) => this.storeSession(response)));
  }

  register(credentials: RegisterRequest): Observable<void> {
    return this.api
      .post<RegisterRequest, void>('/register', credentials)
      .pipe(switchMap(() => this.login({ email: credentials.email, password: credentials.password })));
  }

  logout(): void {
    this.tokenService.clearToken();
    this._loggedIn.set(false);
  }

  private storeSession(response: LoginResponse): void {
    const expiresAt = Date.now() + response.expiresIn * 1000;
    this.tokenService.saveToken(response.accessToken, expiresAt, response.refreshToken);
    this._loggedIn.set(true);
  }
}
