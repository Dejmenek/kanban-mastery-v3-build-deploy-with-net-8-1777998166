import { Component, input, output } from '@angular/core';
import { BoardCard } from '../board-card/board-card';
import { CardResponse, ColumnResponse } from '../../models/board.models';
import { CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-board-column',
  templateUrl: './board-column.html',
  styleUrl: './board-column.css',
  imports: [BoardCard, CdkDropList],
})
export class BoardColumn {
  column = input.required<ColumnResponse>();
  dropped = output<CdkDragDrop<CardResponse[]>>();
}
