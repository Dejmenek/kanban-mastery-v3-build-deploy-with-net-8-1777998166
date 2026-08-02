import { Service } from '@angular/core';

const TOKEN_KEY = 'token';
const EXPIRES_AT_KEY = 'expiresAt';
const REFRESH_TOKEN_KEY = 'refreshToken';

@Service()
export class TokenService {
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  getExpiration(): number | null {
    const value = localStorage.getItem(EXPIRES_AT_KEY);
    return value ? Number(value) : null;
  }

  isExpired(): boolean {
    const expiresAt = this.getExpiration();
    return expiresAt === null || Date.now() >= expiresAt;
  }

  isTokenValid(): boolean {
    return this.getToken() !== null && !this.isExpired();
  }

  saveToken(token: string, expiresAt: number, refreshToken: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(EXPIRES_AT_KEY, expiresAt.toString());
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }

  clearToken(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRES_AT_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }
}
