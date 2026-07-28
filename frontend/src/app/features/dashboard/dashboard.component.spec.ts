import { DestroyRef } from '@angular/core';
import { fakeAsync, tick } from '@angular/core/testing';
import { Subject } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  it('caps 301 realtime news items at 300', () => {
    const component = new DashboardComponent();

    for (let id = 1; id <= 301; id += 1) {
      component.prependNews(createNews(id));
    }

    expect(component.items().length).toBe(300);
    expect(component.items()[0].id).toBe(301);
    expect(component.items()[299].id).toBe(2);
  });

  it('keeps hub news that arrives before the initial HTTP response', fakeAsync(() => {
    const initialNews = new Subject<NewsItem[]>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(initialNews, hubEvents);

    void component.ngOnInit();
    hubEvents.next(createEvent('event-401', 401));
    tick(250);
    initialNews.next([createNews(400)]);

    expect(component.items().map((item) => item.id)).toEqual([401, 400]);
  }));

  it('deduplicates repeated event IDs in one realtime batch', fakeAsync(() => {
    const initialNews = new Subject<NewsItem[]>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(initialNews, hubEvents);

    void component.ngOnInit();
    hubEvents.next(createEvent('event-401', 401));
    hubEvents.next(createEvent('event-401', 402));
    hubEvents.next(createEvent('event-403', 403));
    tick(250);

    expect(component.items().map((item) => item.id)).toEqual([401, 403]);
  }));

  it('continues loading when the hub connection is rejected', async () => {
    const initialNews = new Subject<NewsItem[]>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(initialNews, hubEvents, Promise.reject(new Error('Hub unavailable')));

    await expectAsync(component.ngOnInit()).toBeResolved();
  });
});

function createComponent(
  initialNews: Subject<NewsItem[]>,
  hubEvents: Subject<NewsCreatedEvent>,
  connectionResult: Promise<void> = Promise.resolve(),
): DashboardComponent {
  return new DashboardComponent(
    createDestroyRef(),
    { getLatest: () => initialNews.asObservable() } as never,
    { newsCreated$: hubEvents.asObservable(), connect: () => connectionResult } as never,
  );
}

function createDestroyRef(): DestroyRef {
  return { destroyed: false, onDestroy: () => () => undefined } as unknown as DestroyRef;
}

function createEvent(eventId: string, id: number): NewsCreatedEvent {
  return { eventId, sentAtUtc: '2026-07-28T00:00:00.000Z', news: createNews(id) };
}

function createNews(id: number): NewsItem {
  return {
    id,
    title: `News ${id}`,
    summary: null,
    sourceCode: 'TEST',
    url: 'https://example.com/news',
    publishedAtUtc: '2026-07-28T00:00:00.000Z',
    tickers: ['SPY'],
    sentiment: 'Neutral',
    impactScore: 1,
    tags: [],
  };
}
