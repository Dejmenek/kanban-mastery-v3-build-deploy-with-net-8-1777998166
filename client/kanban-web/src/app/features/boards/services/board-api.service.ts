import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/http/services/api.service';
import { BoardSummaryResponse, BoardDetailsResponse, CreateBoardRequest, UpdateBoardRequest, BoardResponse } from '../models/board.models';

@Service()
export class BoardApiService {
  private api = inject(ApiService);

  getAll(): Observable<BoardSummaryResponse[]> {
    return this.api
      .get<BoardSummaryResponse[]>('/api/v1/boards');
  }

  getById(boardId: number): Observable<BoardDetailsResponse> {
    return this.api
      .get<BoardDetailsResponse>(`/api/v1/boards/${boardId}`);
  }

  createBoard(request: CreateBoardRequest): Observable<BoardSummaryResponse> {
    return this.api
      .post<CreateBoardRequest, BoardSummaryResponse>(`/api/v1/boards`, request);
  }

  deleteBoard(boardId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/v1/boards/${boardId}`);
  }

  updateBoard(boardId: number, request: UpdateBoardRequest): Observable<BoardResponse> {
    return this.api
      .put<UpdateBoardRequest, BoardResponse>(`/api/v1/boards/${boardId}`, request);
  }
}
