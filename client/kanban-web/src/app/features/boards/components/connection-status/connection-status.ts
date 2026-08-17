import { Component, computed, inject } from '@angular/core';
import { BoardService } from '../../services/board.service';

@Component({
  selector: 'app-connection-status',
  templateUrl: './connection-status.html',
  styleUrl: './connection-status.css',
})
export class ConnectionStatus {
  private boardService = inject(BoardService);

  protected state = this.boardService.connectionState;

  protected label = computed(() => {
    switch (this.state()) {
      case 'connected':
        return 'Live';
      case 'reconnecting':
      case 'connecting':
        return 'Reconnecting…';
      case 'disconnected':
        return 'Offline';
    }
  });
}
