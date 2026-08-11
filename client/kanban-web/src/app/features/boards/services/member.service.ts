import { inject, Service } from '@angular/core';
import { ApiService } from '../../../core/http/services/api.service';
import { Observable } from 'rxjs';
import { AddBoardMemberRequest, BoardMemberResponse } from '../models/board.models';

@Service()
export class MemberService {
  private api = inject(ApiService);

  add(boardId: number, request: AddBoardMemberRequest): Observable<BoardMemberResponse> {
    return this.api
      .post<AddBoardMemberRequest, BoardMemberResponse>(`/api/boards/${boardId}/members`, request);
  }

  search(boardId: number, query: string): Observable<BoardMemberResponse[]> {
    return this.api
      .get<BoardMemberResponse[]>(`/api/boards/${boardId}/members/search`, { params: { query } });
  }
}
