import { Component, computed, inject } from '@angular/core';
import { BoardFacade } from '../../services/board-facade.service';

@Component({
  selector: 'app-connection-status',
  templateUrl: './connection-status.html',
  styleUrl: './connection-status.css',
})
export class ConnectionStatus {
  private boardFacade = inject(BoardFacade);

  protected state = this.boardFacade.connectionState;

  protected label = computed(() => {
    switch (this.state().status) {
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
