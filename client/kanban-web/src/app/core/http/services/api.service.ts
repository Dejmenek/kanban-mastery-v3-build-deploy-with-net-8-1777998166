import { Service, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';


@Service()
export class ApiService {
  private baseUrl = environment.apiUrl;
  private http = inject(HttpClient);

  get<T>(path: string) {
    return this.http.get<T>(`${this.baseUrl}${path}`);
  }
}
