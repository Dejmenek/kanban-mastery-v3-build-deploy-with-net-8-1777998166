import { Component, input } from '@angular/core';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { CardResponse } from '../../models/board.models';

@Component({
  selector: 'app-board-card',
  templateUrl: './board-card.html',
  styleUrl: './board-card.css',
  imports: [Avatar],
})
export class BoardCard {
  card = input.required<CardResponse>();
}
