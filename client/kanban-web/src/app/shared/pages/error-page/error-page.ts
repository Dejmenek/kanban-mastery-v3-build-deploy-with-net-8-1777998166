import { Component, input } from '@angular/core';
import { StatusPage } from '../../components/status-page/status-page';

@Component({
  selector: 'app-error-page',
  templateUrl: './error-page.html',
  styleUrl: './error-page.css',
  imports: [StatusPage],
})
export class ErrorPage {
  message = input('Something went wrong. Please try again.');
}
