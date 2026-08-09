import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { AddBoardMemberRequest, BoardDetailsResponse, BoardMemberResponse, BoardResponse, BoardSummaryResponse, CardResponse, CreateBoardRequest, CreateCardRequest, MoveCardRequest, MoveCardResponse, UpdateBoardRequest } from '../models/board.models';

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

  createBoard(request: CreateBoardRequest) {
    return this.api
      .post<CreateBoardRequest, BoardSummaryResponse>(`/api/boards`, request);
  }

  updateBoard(boardId: number, request: UpdateBoardRequest) {
    return this.api
      .put<UpdateBoardRequest, BoardResponse>(`/api/boards/${boardId}`, request);
  }

  deleteBoard(boardId: number) {
    return this.api
      .delete<void>(`/api/boards/${boardId}`);
  }
}
