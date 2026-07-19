import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { ApiValidationError } from '../models/api-problem-details';
import { NewsApiService } from './news-api.service';

describe('NewsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('gets the latest news using the default limit', () => {
    const service = TestBed.inject(NewsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getLatest().subscribe();

    const request = httpTesting.expectOne('http://localhost:5179/api/news/latest?limit=30');
    expect(request.request.method).toBe('GET');
    request.flush([]);
    httpTesting.verify();
  });

  it('maps a validation response to ApiValidationError', (done: DoneFn) => {
    const service = TestBed.inject(NewsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getLatest(0).subscribe({
      next: () => fail('Expected a validation error.'),
      error: (error: unknown) => {
        expect(error).toEqual(jasmine.any(ApiValidationError));
        expect((error as ApiValidationError).problem.errors).toEqual({ limit: ['must be between 1 and 200'] });
        done();
      },
    });

    const request = httpTesting.expectOne('http://localhost:5179/api/news/latest?limit=0');
    request.flush(
      { title: 'One or more query parameters are invalid.', status: 400, errors: { limit: ['must be between 1 and 200'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    httpTesting.verify();
  });
});
