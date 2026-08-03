import { Component, input } from '@angular/core';
import { BoardCard } from '../board-card/board-card';
import { ColumnResponse } from '../../models/board.models';

@Component({
  selector: 'app-board-column',
  templateUrl: './board-column.html',
  styleUrl: './board-column.css',
  imports: [BoardCard],
})
export class BoardColumn {
  column = input.required<ColumnResponse>();
}
