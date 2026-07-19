import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { WatchlistApiService } from './watchlist-api.service';

describe('WatchlistApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('gets all watchlist items', () => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getAll().subscribe();

    const request = httpTesting.expectOne('http://localhost:5000/api/watchlist');
    expect(request.request.method).toBe('GET');
    request.flush([]);
    httpTesting.verify();
  });

  it('adds a watchlist item', () => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const requestBody = { ticker: 'NVDA', displayName: 'NVIDIA', market: 'NASDAQ' };

    service.add(requestBody).subscribe();

    const request = httpTesting.expectOne('http://localhost:5000/api/watchlist');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({});
    httpTesting.verify();
  });

  it('removes a watchlist item using an encoded ticker', () => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.remove('BRK/B').subscribe();

    const request = httpTesting.expectOne('http://localhost:5000/api/watchlist/BRK%2FB');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    httpTesting.verify();
  });
});
