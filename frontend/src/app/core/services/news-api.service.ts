import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { NewsItem } from '../models/news-item';
import { rethrowApiError } from './api-error-handler';

@Injectable({ providedIn: 'root' })
export class NewsApiService {
  private readonly http = inject(HttpClient);

  getLatest(limit = 30): Observable<NewsItem[]> {
    return this.http
      .get<NewsItem[]>(`${environment.apiBaseUrl}/api/news/latest`, {
        params: new HttpParams().set('limit', limit),
      })
      .pipe(catchError((error: unknown) => rethrowApiError<NewsItem[]>(error)));
  }
}
