import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { CreateWatchlistRequest, WatchlistItem } from '../models/watchlist-item';
import { rethrowApiError } from './api-error-handler';

@Injectable({ providedIn: 'root' })
export class WatchlistApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/watchlist`;

  getAll(): Observable<WatchlistItem[]> {
    return this.http
      .get<WatchlistItem[]>(this.endpoint)
      .pipe(catchError((error: unknown) => rethrowApiError<WatchlistItem[]>(error)));
  }

  add(request: CreateWatchlistRequest): Observable<WatchlistItem> {
    return this.http
      .post<WatchlistItem>(this.endpoint, request)
      .pipe(catchError((error: unknown) => rethrowApiError<WatchlistItem>(error)));
  }

  remove(ticker: string): Observable<void> {
    return this.http
      .delete<void>(`${this.endpoint}/${encodeURIComponent(ticker)}`)
      .pipe(catchError((error: unknown) => rethrowApiError<void>(error)));
  }
}
