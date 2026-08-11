import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/http/services/api.service';
import { CreateColumnRequest, ColumnResponse } from '../models/board.models';

@Service()
export class ColumnService {
  private api = inject(ApiService);

  create(boardId: number, request: CreateColumnRequest): Observable<ColumnResponse> {
    return this.api
      .post<CreateColumnRequest, ColumnResponse>(`/api/boards/${boardId}/columns`, request);
  }
}
