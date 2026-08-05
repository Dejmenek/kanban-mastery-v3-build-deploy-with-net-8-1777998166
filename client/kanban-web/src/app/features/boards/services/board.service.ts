import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { AddBoardMemberRequest, BoardDetailsResponse, BoardMemberResponse, BoardSummaryResponse, CardResponse, CreateCardRequest, MoveCardRequest, MoveCardResponse } from '../models/board.models';

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

  moveCard(boardId: number, cardId: number, request: MoveCardRequest): Observable<MoveCardResponse> {
    return this.api
      .put<MoveCardRequest, MoveCardResponse>(`/api/boards/${boardId}/cards/${cardId}/position`, request);
  }

  addMember(boardId: number, request: AddBoardMemberRequest) {
    return this.api
      .post<AddBoardMemberRequest, BoardMemberResponse>(`/api/boards/${boardId}/members`, request);
  }

  createCard(boardId: number, request: CreateCardRequest) {
    return this.api
      .post<CreateCardRequest, CardResponse>(`/api/boards/${boardId}/cards`, request);
  }
}
