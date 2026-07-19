import { TestBed } from '@angular/core/testing';

import { NewsHubService } from './news-hub.service';

describe('NewsHubService', () => {
  it('creates a service', () => {
    expect(TestBed.inject(NewsHubService)).toBeTruthy();
  });
});
