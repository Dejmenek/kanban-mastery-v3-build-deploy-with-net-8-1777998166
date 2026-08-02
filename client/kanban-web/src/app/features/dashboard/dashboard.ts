import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BoardService } from '../boards/services/board.service';
import { rxResource } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
  imports: [RouterLink],
})
export class Dashboard {
  private boardService = inject(BoardService);

  protected boardsResource = rxResource({
    stream: () => this.boardService.getAll()
  });
}
