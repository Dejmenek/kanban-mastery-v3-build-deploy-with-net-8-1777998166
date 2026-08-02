import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth/services/auth.service';

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
  imports: [RouterLink, RouterLinkActive],
})
export class Sidebar {
  protected auth = inject(AuthService);
  private router = inject(Router);

  protected collapsed = signal(false);

  protected toggleCollapsed(): void {
    this.collapsed.update((v) => !v);
  }

  protected onLogout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
