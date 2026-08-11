import { Component, input } from '@angular/core';

@Component({
  selector: 'app-error-message',
  templateUrl: './error-message.html',
})
export class ErrorMessage {
  message = input<string | null>(null);
}
