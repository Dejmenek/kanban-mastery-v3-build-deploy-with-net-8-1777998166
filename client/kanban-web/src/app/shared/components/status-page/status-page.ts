import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-status-page',
  templateUrl: './status-page.html',
  styleUrl: './status-page.css',
  imports: [RouterLink],
})
export class StatusPage {
  code = input.required<string>();
  heading = input.required<string>();
  message = input.required<string>();
  actionLabel = input('Back to home');
  actionLink = input('/');
  tone = input<'error' | 'warning'>('error');
}
