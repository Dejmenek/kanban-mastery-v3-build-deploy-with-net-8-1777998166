import { Component, input } from '@angular/core';
import { BoardColumn } from '../../components/board-column/board-column';
import { BoardDetailsResponse } from '../../models/board.models';

@Component({
  selector: 'app-board-detail',
  templateUrl: './board-detail.html',
  styleUrl: './board-detail.css',
  imports: [BoardColumn],
})
export class BoardDetail {
  board = input.required<BoardDetailsResponse>();
}
