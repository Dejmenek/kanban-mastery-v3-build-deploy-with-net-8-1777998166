import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { BoardDetailsResponse, BoardSummaryResponse } from '../models/board.models';

@Service()
export class BoardService {
  private api = inject(ApiService);

  getAll(): Observable<BoardSummaryResponse[]> {
    return this.api
      .get<BoardSummaryResponse[]>('/api/boards');
  }

  getById(boardId: number): Observable<BoardDetailsResponse> {
    return this.api
      .get<BoardDetailsResponse>(`/api/boards/${boardId}`);
  }
}
