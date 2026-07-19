import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { CreateWatchlistRequest, WatchlistItem } from '../models/watchlist-item';

@Injectable({ providedIn: 'root' })
export class WatchlistApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/watchlist`;

  getAll(): Observable<WatchlistItem[]> {
    return this.http.get<WatchlistItem[]>(this.endpoint);
  }

  add(request: CreateWatchlistRequest): Observable<WatchlistItem> {
    return this.http.post<WatchlistItem>(this.endpoint, request);
  }

  remove(ticker: string): Observable<void> {
    return this.http.delete<void>(`${this.endpoint}/${encodeURIComponent(ticker)}`);
  }
}
