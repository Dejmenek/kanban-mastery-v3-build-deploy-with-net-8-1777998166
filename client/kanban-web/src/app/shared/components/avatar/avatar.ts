import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-avatar',
  templateUrl: './avatar.html',
  styleUrl: './avatar.css',
})
export class Avatar {
  name = input<string | null>(null);
  email = input<string | null>(null);

  protected initials = computed(() => {
    const name = this.name()?.trim();
    if (name) return this.fromName(name);

    const email = this.email()?.trim();
    if (email) return this.fromEmail(email);

    return null;
  });

  private fromName(name: string): string {
    const parts = name.split(/\s+/).filter(Boolean);
    return parts.length === 1
      ? parts[0].slice(0, 2).toUpperCase()
      : (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  private fromEmail(email: string): string {
    const local = email.split('@')[0];
    const segments = local.split(/[._-]+/).filter(Boolean);
    return segments.length >= 2
      ? (segments[0][0] + segments[1][0]).toUpperCase()
      : local.slice(0, 2).toUpperCase();
  }
}
