import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

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

    const request = httpTesting.expectOne('http://localhost:5000/api/news/latest?limit=30');
    expect(request.request.method).toBe('GET');
    request.flush([]);
    httpTesting.verify();
  });
});
