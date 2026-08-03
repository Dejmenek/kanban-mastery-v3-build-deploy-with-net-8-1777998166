import { Component } from '@angular/core';
import { StatusPage } from '../../components/status-page/status-page';

@Component({
  selector: 'app-not-found',
  templateUrl: './not-found.html',
  styleUrl: './not-found.css',
  imports: [StatusPage],
})
export class NotFound {}
