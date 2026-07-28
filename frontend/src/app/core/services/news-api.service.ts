import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { NewsItem } from '../models/news-item';
import { NewsQuery, PagedNewsResponse } from '../models/news-query';
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

  query(query: NewsQuery): Observable<PagedNewsResponse> {
    const params = new HttpParams({
      fromObject: {
        ...(query.ticker ? { ticker: query.ticker } : {}),
        ...(query.sourceCode ? { sourceCode: query.sourceCode } : {}),
        ...(query.sentiment ? { sentiment: query.sentiment } : {}),
        ...(query.tag ? { tag: query.tag } : {}),
        page: query.page.toString(),
        pageSize: query.pageSize.toString(),
        sortBy: query.sortBy,
        watchlistOnly: query.watchlistOnly.toString(),
      },
    });

    return this.http
      .get<PagedNewsResponse>(`${environment.apiBaseUrl}/api/news`, { params })
      .pipe(catchError((error: unknown) => rethrowApiError<PagedNewsResponse>(error)));
  }
}
