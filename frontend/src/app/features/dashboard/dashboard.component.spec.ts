import { DestroyRef } from '@angular/core';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { HubConnectionState } from '@microsoft/signalr';
import { By } from '@angular/platform-browser';
import { of, Subject } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { NewsApiService } from '../../core/services/news-api.service';
import { NewsHubService } from '../../core/services/news-hub.service';
import { WatchlistApiService } from '../../core/services/watchlist-api.service';
import { DashboardComponent } from './dashboard.component';
import { NewsFeedComponent } from './news-feed.component';
import { NewsInspectorComponent } from './news-inspector.component';

describe('DashboardComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: NewsApiService, useValue: { getLatest: () => of([]) } },
        {
          provide: NewsHubService,
          useValue: {
            newsCreated$: of(),
            connect: () => Promise.resolve(),
            connectionState: () => HubConnectionState.Disconnected,
          },
        },
        { provide: WatchlistApiService, useValue: { getAll: () => of([]) } },
      ],
    });
  });

  it('keeps the watchlist reachable through an accessible disclosure', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const summary = fixture.nativeElement.querySelector('summary') as HTMLElement;
    const feed = fixture.nativeElement.querySelector('[aria-label="Realtime news feed"]') as HTMLElement;

    expect(summary.textContent?.trim()).toContain('Watchlist');
    expect(feed).not.toBeNull();
  });

  it('reopens a collapsed mobile watchlist after resizing to the desktop layout', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    const disclosure = fixture.nativeElement.querySelector('.dashboard__watchlist') as HTMLDetailsElement;
    disclosure.open = false;
    const innerWidth = spyOnProperty(window, 'innerWidth', 'get').and.returnValue(719);

    window.dispatchEvent(new Event('resize'));
    expect(disclosure.open).toBeFalse();

    innerWidth.and.returnValue(720);
    window.dispatchEvent(new Event('resize'));

    expect(disclosure.open).toBeTrue();
  });

  it('loads the initial feed with the API default limit while retaining the larger in-memory cap', async () => {
    const initialNews = new Subject<NewsItem[]>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    let requestedLimit: number | undefined;
    const component = new DashboardComponent(
      createDestroyRef(),
      {
        getLatest: (limit: number) => {
          requestedLimit = limit;
          return initialNews.asObservable();
        },
      } as never,
      { newsCreated$: hubEvents.asObservable(), connect: () => Promise.resolve() } as never,
    );

    await component.ngOnInit();

    expect(requestedLimit).toBe(30);
  });

  it('caps 301 realtime news items at 300', () => {
    const component = new DashboardComponent();

    for (let id = 1; id <= 301; id += 1) {
      component.prependNews(createNews(id));
    }

    expect(component.items().length).toBe(300);
    expect(component.items()[0].id).toBe(301);
    expect(component.items()[299].id).toBe(2);
  });

  it('stores the news selected from the feed', () => {
    const component = new DashboardComponent();
    const news = createNews(99);

    component.selectNews(news);

    expect(component.selectedNews()).toBe(news);
  });

  it('propagates feed selection to the selected feed state and inspector', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    const news = createNews(100);
    fixture.componentInstance.items.set([news]);
    fixture.detectChanges();
    const feed = fixture.debugElement.query(By.directive(NewsFeedComponent)).componentInstance as NewsFeedComponent;

    feed.newsSelected.emit(news);
    fixture.detectChanges();

    const inspector = fixture.debugElement.query(By.directive(NewsInspectorComponent)).componentInstance as NewsInspectorComponent;
    expect(fixture.componentInstance.selectedNews()).toBe(news);
    expect(feed.selectedNewsId()).toBe(news.id);
    expect(inspector.news()).toBe(news);
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
