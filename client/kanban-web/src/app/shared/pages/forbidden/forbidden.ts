import { Component } from '@angular/core';
import { StatusPage } from '../../components/status-page/status-page';

@Component({
  selector: 'app-forbidden',
  templateUrl: './forbidden.html',
  styleUrl: './forbidden.css',
  imports: [StatusPage],
})
export class Forbidden {}
