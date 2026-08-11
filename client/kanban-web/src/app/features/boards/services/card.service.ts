import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { AssignCardRequest, CardResponse, CreateCardRequest, MoveCardRequest, MoveCardResponse } from '../models/board.models';

@Service()
export class CardService {
  private api = inject(ApiService);

  create(boardId: number, request: CreateCardRequest): Observable<CardResponse> {
    return this.api
      .post<CreateCardRequest, CardResponse>(`/api/boards/${boardId}/cards`, request);
  }

  delete(boardId: number, cardId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/boards/${boardId}/cards/${cardId}`);
  }

  move(boardId: number, cardId: number, request: MoveCardRequest): Observable<MoveCardResponse> {
    return this.api
      .put<MoveCardRequest, MoveCardResponse>(`/api/boards/${boardId}/cards/${cardId}/position`, request);
  }

  assign(boardId: number, cardId: number, request: AssignCardRequest): Observable<CardResponse> {
    return this.api
      .put<AssignCardRequest, CardResponse>(`/api/boards/${boardId}/cards/${cardId}/assign`, request);
  }
}
