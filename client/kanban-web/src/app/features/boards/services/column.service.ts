import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/http/services/api.service';
import { CreateColumnRequest, ColumnResponse, UpdateColumnRequest, MoveColumnRequest, MoveColumnResponse } from '../models/board.models';

@Service()
export class ColumnService {
  private api = inject(ApiService);

  create(boardId: number, request: CreateColumnRequest): Observable<ColumnResponse> {
    return this.api
      .post<CreateColumnRequest, ColumnResponse>(`/api/boards/${boardId}/columns`, request);
  }

  delete(boardId: number, columnId: number): Observable<void> {
    return this.api
      .delete<void>(`/api/boards/${boardId}/columns/${columnId}`);
  }

  update(boardId: number, columnId: number, request: UpdateColumnRequest): Observable<ColumnResponse> {
    return this.api
      .put<UpdateColumnRequest, ColumnResponse>(`/api/boards/${boardId}/columns/${columnId}`, request);
  }

  move(boardId: number, columnId: number, request: MoveColumnRequest): Observable<MoveColumnResponse> {
    return this.api
      .put<MoveColumnRequest, MoveColumnResponse>(`/api/boards/${boardId}/columns/${columnId}/position`, request);
  }
}
