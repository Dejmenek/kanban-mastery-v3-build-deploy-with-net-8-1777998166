import { Service, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient, HttpClientCommonOptions } from '@angular/common/http';
import { Observable } from 'rxjs';


@Service()
export class ApiService {
  private baseUrl = environment.apiUrl;
  private http = inject(HttpClient);

  get<T>(path: string) {
    return this.http.get<T>(`${this.baseUrl}${path}`);
  }

  post<T, R = T>(path: string, body: T, options?: HttpClientCommonOptions): Observable<R> {
    return this.http.post<R>(`${this.baseUrl}${path}`, body, options);
  }
}
