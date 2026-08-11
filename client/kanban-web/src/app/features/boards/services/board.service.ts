import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { AddBoardMemberRequest, AssignCardRequest, BoardDetailsResponse, BoardMemberResponse, BoardResponse, BoardSummaryResponse, CardResponse, CreateBoardRequest, CreateCardRequest, MoveCardRequest, MoveCardResponse, UpdateBoardRequest } from '../models/board.models';
import { extractErrorMessage } from '../../../shared/utils/extract-error-message';

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

  addMember(boardId: number, request: AddBoardMemberRequest): Observable<BoardMemberResponse> {
    return this.api
      .post<AddBoardMemberRequest, BoardMemberResponse>(`/api/boards/${boardId}/members`, request);
  }

  createCard(boardId: number, request: CreateCardRequest): Observable<CardResponse> {
    return this.api
      .post<CreateCardRequest, CardResponse>(`/api/boards/${boardId}/cards`, request);
  }

  createBoard(request: CreateBoardRequest): Observable<BoardSummaryResponse> {
    return this.api
      .post<CreateBoardRequest, BoardSummaryResponse>(`/api/boards`, request);
  }

  updateBoard(boardId: number, request: UpdateBoardRequest): Observable<BoardResponse> {
    return this.api
      .put<UpdateBoardRequest, BoardResponse>(`/api/boards/${boardId}`, request);
  }

  deleteBoard(boardId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/boards/${boardId}`);
  }

  deleteCard(boardId: number, cardId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/boards/${boardId}/cards/${cardId}`);
  }

  searchMembers(boardId: number, query: string): Observable<BoardMemberResponse[]> {
    return this.api
      .get<BoardMemberResponse[]>(`/api/boards/${boardId}/members/search`, { params: { query } });

  }

  assignMember(boardId: number, cardId: number, request: AssignCardRequest): Observable<CardResponse> {
    return this.api
      .put<AssignCardRequest, CardResponse>(`/api/boards/${boardId}/cards/${cardId}/assign`, request);
  }
}
