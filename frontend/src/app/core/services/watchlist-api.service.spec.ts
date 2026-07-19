import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { ApiValidationError } from '../models/api-problem-details';
import { environment } from '../../../environments/environment.development';
import { WatchlistApiService } from './watchlist-api.service';

const watchlistEndpoint = `${environment.apiBaseUrl}/api/watchlist`;

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

    const request = httpTesting.expectOne(watchlistEndpoint);
    expect(request.request.method).toBe('GET');
    request.flush([]);
    httpTesting.verify();
  });

  it('adds a watchlist item', () => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const requestBody = { ticker: 'NVDA', displayName: 'NVIDIA', market: 'NASDAQ' };

    service.add(requestBody).subscribe();

    const request = httpTesting.expectOne(watchlistEndpoint);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({});
    httpTesting.verify();
  });

  it('removes a watchlist item using an encoded ticker', () => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.remove('BRK/B').subscribe();

    const request = httpTesting.expectOne(`${watchlistEndpoint}/BRK%2FB`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    httpTesting.verify();
  });

  it('maps a validation response to ApiValidationError', (done: DoneFn) => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.add({ ticker: '', displayName: null, market: null }).subscribe({
      next: () => fail('Expected a validation error.'),
      error: (error: unknown) => {
        expect(error).toEqual(jasmine.any(ApiValidationError));
        expect((error as ApiValidationError).problem.title).toBe('Ticker is required.');
        done();
      },
    });

    const request = httpTesting.expectOne(watchlistEndpoint);
    request.flush(
      { title: 'Ticker is required.', status: 400 },
      { status: 400, statusText: 'Bad Request' },
    );
    httpTesting.verify();
  });

  it('preserves malformed validation payloads as HttpErrorResponse', (done: DoneFn) => {
    const service = TestBed.inject(WatchlistApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.add({ ticker: '', displayName: null, market: null }).subscribe({
      next: () => fail('Expected a validation error.'),
      error: (error: unknown) => {
        expect(error).toEqual(jasmine.any(HttpErrorResponse));
        done();
      },
    });

    const request = httpTesting.expectOne(watchlistEndpoint);
    request.flush(
      { title: 'Ticker is required.', status: 400, errors: null },
      { status: 400, statusText: 'Bad Request' },
    );
    httpTesting.verify();
  });
});
