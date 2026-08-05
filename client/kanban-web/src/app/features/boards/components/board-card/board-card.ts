import { Component, input } from '@angular/core';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { CardResponse } from '../../models/board.models';
import { CdkDrag } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-board-card',
  templateUrl: './board-card.html',
  styleUrl: './board-card.css',
  imports: [Avatar, CdkDrag],
})
export class BoardCard {
  card = input.required<CardResponse>();
}
