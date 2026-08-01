import { Service } from '@angular/core';

@Service()
export class JwtService {
  getToken(): string | null {
    return localStorage.getItem('token');
  }

  saveToken(token: string): void {
    localStorage.setItem('token', token);
  }
}
