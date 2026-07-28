import { DestroyRef } from '@angular/core';
import { fakeAsync, tick } from '@angular/core/testing';
import { convertToParamMap } from '@angular/router';
import { Observable, Subject } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { NewsQuery, PagedNewsResponse } from '../../core/models/news-query';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  it('loads the initial feed with the exact default paged query', async () => {
    const requests: NewsQuery[] = [];
    const component = createComponent((query) => {
      requests.push(query);
      return new Subject<PagedNewsResponse>();
    });

    await component.ngOnInit();

    expect(requests).toEqual([createQuery()]);
  });

  it('restores valid filters, sort, and page from the URL before loading', async () => {
    const requests: NewsQuery[] = [];
    const component = createComponent(
      (query) => {
        requests.push(query);
        return new Subject<PagedNewsResponse>();
      },
      new Subject<NewsCreatedEvent>(),
      Promise.resolve(),
      {
        ticker: ' AAPL ',
        sourceCode: ' mock ',
        sentiment: 'Negative',
        tag: ' earnings ',
        page: '2',
        sortBy: 'impact',
        watchlistOnly: 'true',
      },
    );

    await component.ngOnInit();

    expect(requests[0]).toEqual({
      ticker: 'AAPL',
      sourceCode: 'mock',
      sentiment: 'Negative',
      tag: 'earnings',
      page: 2,
      pageSize: 30,
      sortBy: 'impact',
      watchlistOnly: true,
    });
  });

  it('ignores invalid URL query values instead of sending an invalid API request', async () => {
    const requests: NewsQuery[] = [];
    const component = createComponent(
      (query) => {
        requests.push(query);
        return new Subject<PagedNewsResponse>();
      },
      new Subject<NewsCreatedEvent>(),
      Promise.resolve(),
      {
        ticker: 'bad ticker!',
        sentiment: 'Mixed',
        page: '0',
        sortBy: 'title',
        watchlistOnly: 'yes',
      },
    );

    await component.ngOnInit();

    expect(requests[0]).toEqual(createQuery());
  });

  it('resets to page one and requests impact order with Negative sentiment after a filter change', () => {
    const requests: NewsQuery[] = [];
    const component = createComponent((query) => {
      requests.push(query);
      return new Subject<PagedNewsResponse>();
    });

    component.updateQuery({ page: 4, sortBy: 'impact', sentiment: 'Negative' });

    expect(requests.at(-1)).toEqual(jasmine.objectContaining({
      page: 1,
      sortBy: 'impact',
      sentiment: 'Negative',
    }));
  });

  it('trims text filters and writes only non-default values to the URL', () => {
    const navigations: unknown[] = [];
    const component = createComponent(
      () => new Subject<PagedNewsResponse>(),
      new Subject<NewsCreatedEvent>(),
      Promise.resolve(),
      {},
      navigations,
    );

    component.updateQuery({ ticker: ' AAPL ', sourceCode: '  ', tag: ' earnings ', sortBy: 'impact' });

    expect(component.query()).toEqual(jasmine.objectContaining({
      ticker: 'AAPL',
      sourceCode: null,
      tag: 'earnings',
      page: 1,
      sortBy: 'impact',
    }));
    expect(navigations.at(-1)).toEqual(jasmine.objectContaining({
      queryParams: { ticker: 'AAPL', tag: 'earnings', sortBy: 'impact' },
    }));
  });

  it('clears query state, URL parameters, and reloads page one', () => {
    const requests: NewsQuery[] = [];
    const navigations: unknown[] = [];
    const component = createComponent(
      (query) => {
        requests.push(query);
        return new Subject<PagedNewsResponse>();
      },
      new Subject<NewsCreatedEvent>(),
      Promise.resolve(),
      {},
      navigations,
    );
    component.updateQuery({ ticker: 'AAPL', sortBy: 'impact' });

    component.clearQuery();

    expect(component.query()).toEqual(createQuery());
    expect(requests.at(-1)).toEqual(createQuery());
    expect(navigations.at(-1)).toEqual(jasmine.objectContaining({ queryParams: {} }));
  });

  it('appends a next page without duplicate news and updates paging state', async () => {
    const responses: Subject<PagedNewsResponse>[] = [];
    const requested: NewsQuery[] = [];
    const component = createComponent((query) => {
      requested.push(query);
      const response = new Subject<PagedNewsResponse>();
      responses.push(response);
      return response;
    });
    await component.ngOnInit();
    responses[0].next(createResponse([createNews(2), createNews(1)], 1, 4, true));
    responses[0].complete();

    component.loadNextPage();
    component.loadNextPage();
    expect(requested.length).toBe(2);
    responses[1].next(createResponse([createNews(3), createNews(2)], 2, 4, false));
    responses[1].complete();

    expect(requested[1].page).toBe(2);
    expect(component.query().page).toBe(2);
    expect(component.items().map((item) => item.id)).toEqual([3, 2, 1]);
    expect(component.totalCount()).toBe(4);
    expect(component.hasMore()).toBeFalse();
  });

  it('ignores an obsolete page-one response after the query changes', async () => {
    const responses: Subject<PagedNewsResponse>[] = [];
    const component = createComponent(() => {
      const response = new Subject<PagedNewsResponse>();
      responses.push(response);
      return response;
    });
    await component.ngOnInit();
    component.updateQuery({ ticker: 'AAPL' });

    responses[1].next(createResponse([createNews(2, { tickers: ['AAPL'] })]));
    responses[0].next(createResponse([createNews(1, { tickers: ['NVDA'] })]));

    expect(component.items().map((item) => item.id)).toEqual([2]);
  });

  it('keeps a possible count after a replacement request retains items and fails', async () => {
    const responses: Subject<PagedNewsResponse>[] = [];
    const component = createComponent(() => {
      const response = new Subject<PagedNewsResponse>();
      responses.push(response);
      return response;
    });
    await component.ngOnInit();
    responses[0].next(createResponse([
      createNews(2, { tickers: ['AAPL'] }),
      createNews(1, { tickers: ['AAPL'] }),
    ], 1, 10));
    responses[0].complete();

    component.updateQuery({ ticker: 'AAPL' });
    responses[1].error(new Error('Query failed'));

    expect(component.items().map((item) => item.id)).toEqual([2, 1]);
    expect(component.totalCount()).toBe(2);
    expect(component.errorMessage()).toBe('ไม่สามารถโหลดข่าวได้');
  });

  it('does not insert a buffered NVDA event while ticker AAPL is selected', fakeAsync(() => {
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => new Subject<PagedNewsResponse>(), hubEvents);

    void component.ngOnInit();
    component.updateQuery({ ticker: 'AAPL' });
    hubEvents.next(createEvent('event-401', createNews(401, { tickers: ['NVDA'] })));
    tick(250);

    expect(component.items()).toEqual([]);
  }));

  it('requires realtime news to match source, sentiment, tag, and active watchlist together', fakeAsync(() => {
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => new Subject<PagedNewsResponse>(), hubEvents);

    void component.ngOnInit();
    component.updateQuery({
      sourceCode: 'mock',
      sentiment: 'Positive',
      tag: 'earnings',
      watchlistOnly: true,
    });
    component.setActiveWatchlistTickers([' aapl ']);
    hubEvents.next(createEvent('event-match', createNews(1, {
      sourceCode: 'MOCK',
      tickers: ['AAPL'],
      sentiment: 'Positive',
      tags: ['earnings'],
    })));
    hubEvents.next(createEvent('event-source', createNews(2, {
      sourceCode: 'OTHER',
      tickers: ['AAPL'],
      sentiment: 'Positive',
      tags: ['earnings'],
    })));
    hubEvents.next(createEvent('event-sentiment', createNews(3, {
      sourceCode: 'MOCK',
      tickers: ['AAPL'],
      sentiment: 'Negative',
      tags: ['earnings'],
    })));
    hubEvents.next(createEvent('event-tag', createNews(4, {
      sourceCode: 'MOCK',
      tickers: ['AAPL'],
      sentiment: 'Positive',
      tags: ['other'],
    })));
    hubEvents.next(createEvent('event-watchlist', createNews(5, {
      sourceCode: 'MOCK',
      tickers: ['NVDA'],
      sentiment: 'Positive',
      tags: ['earnings'],
    })));
    tick(250);

    expect(component.activeWatchlistTickers()).toEqual(['AAPL']);
    expect(component.items().map((item) => item.id)).toEqual([1]);
  }));

  it('reloads page one when active tickers change in watchlist-only mode', () => {
    const requests: NewsQuery[] = [];
    const component = createComponent((query) => {
      requests.push(query);
      return new Subject<PagedNewsResponse>();
    });
    component.setActiveWatchlistTickers(['AAPL']);
    component.updateQuery({ watchlistOnly: true });

    component.setActiveWatchlistTickers(['NVDA']);

    expect(requests.at(-1)).toEqual(jasmine.objectContaining({ page: 1, watchlistOnly: true }));
    expect(requests.length).toBe(2);
  });

  it('preserves a restored watchlist-only page when active tickers are published initially', async () => {
    const requests: NewsQuery[] = [];
    const component = createComponent(
      (query) => {
        requests.push(query);
        return new Subject<PagedNewsResponse>();
      },
      new Subject<NewsCreatedEvent>(),
      Promise.resolve(),
      { page: '2', watchlistOnly: 'true' },
    );
    await component.ngOnInit();

    component.setActiveWatchlistTickers(['AAPL']);

    expect(component.query().page).toBe(2);
    expect(requests.length).toBe(1);
  });

  it('re-evaluates a watchlist event received before initial active tickers are published', fakeAsync(() => {
    const requests: NewsQuery[] = [];
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(
      (query) => {
        requests.push(query);
        return new Subject<PagedNewsResponse>();
      },
      hubEvents,
      Promise.resolve(),
      { page: '2', watchlistOnly: 'true' },
    );

    void component.ngOnInit();
    hubEvents.next(createEvent('event-aapl', createNews(501, { tickers: ['AAPL'] })));
    tick(250);
    expect(component.items()).toEqual([]);

    component.setActiveWatchlistTickers(['AAPL']);

    expect(component.items().map((item) => item.id)).toEqual([501]);
    expect(component.query().page).toBe(2);
    expect(requests.length).toBe(1);
  }));

  it('keeps total count at least as large as displayed items after matching realtime news', fakeAsync(() => {
    const initialNews = new Subject<PagedNewsResponse>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => initialNews, hubEvents);

    void component.ngOnInit();
    initialNews.next(createResponse([createNews(1)], 1, 1));
    initialNews.complete();
    hubEvents.next(createEvent('event-2', createNews(2)));
    tick(250);

    expect(component.items().map((item) => item.id)).toEqual([2, 1]);
    expect(component.totalCount()).toBe(2);
  }));

  it('orders impact realtime news by publication time and then ID for deterministic ties', fakeAsync(() => {
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => new Subject<PagedNewsResponse>(), hubEvents);

    void component.ngOnInit();
    component.updateQuery({ sortBy: 'impact' });
    hubEvents.next(createEvent('event-1', createNews(1, { impactScore: 0.9, publishedAtUtc: '2026-07-28T10:00:00.000Z' })));
    hubEvents.next(createEvent('event-2', createNews(2, { impactScore: 0.9, publishedAtUtc: '2026-07-28T11:00:00.000Z' })));
    hubEvents.next(createEvent('event-3', createNews(3, { impactScore: 0.9, publishedAtUtc: '2026-07-28T11:00:00.000Z' })));
    hubEvents.next(createEvent('event-4', createNews(4, { impactScore: 1, publishedAtUtc: '2026-07-28T09:00:00.000Z' })));
    tick(250);

    expect(component.items().map((item) => item.id)).toEqual([4, 3, 2, 1]);
  }));

  it('keeps matching hub news that arrives before the initial HTTP response', fakeAsync(() => {
    const initialNews = new Subject<PagedNewsResponse>();
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => initialNews, hubEvents);

    void component.ngOnInit();
    hubEvents.next(createEvent('event-401', createNews(401)));
    tick(250);
    initialNews.next(createResponse([createNews(400)]));

    expect(component.items().map((item) => item.id)).toEqual([401, 400]);
  }));

  it('deduplicates repeated event IDs in one realtime batch', fakeAsync(() => {
    const hubEvents = new Subject<NewsCreatedEvent>();
    const component = createComponent(() => new Subject<PagedNewsResponse>(), hubEvents);

    void component.ngOnInit();
    hubEvents.next(createEvent('event-401', createNews(401)));
    hubEvents.next(createEvent('event-401', createNews(402)));
    hubEvents.next(createEvent('event-403', createNews(403)));
    tick(250);

    expect(component.items().map((item) => item.id)).toEqual([403, 401]);
  }));

  it('caps 301 realtime news items at 300', () => {
    const component = new DashboardComponent();

    for (let id = 1; id <= 301; id += 1) {
      component.prependNews(createNews(id));
    }

    expect(component.items().length).toBe(300);
    expect(component.items()[0].id).toBe(301);
    expect(component.items()[299].id).toBe(2);
  });

  it('continues loading when the hub connection is rejected', async () => {
    const component = createComponent(
      () => new Subject<PagedNewsResponse>(),
      new Subject<NewsCreatedEvent>(),
      Promise.reject(new Error('Hub unavailable')),
    );

    await expectAsync(component.ngOnInit()).toBeResolved();
  });
});

function createComponent(
  queryNews: (query: NewsQuery) => Observable<PagedNewsResponse>,
  hubEvents = new Subject<NewsCreatedEvent>(),
  connectionResult: Promise<void> = Promise.resolve(),
  queryParams: Record<string, string> = {},
  navigations: unknown[] = [],
): DashboardComponent {
  return new DashboardComponent(
    createDestroyRef(),
    { query: queryNews } as never,
    {
      newsCreated$: hubEvents.asObservable(),
      connect: () => connectionResult,
      connectionState: () => 'Connected',
    } as never,
    { snapshot: { queryParamMap: convertToParamMap(queryParams) } } as never,
    {
      navigate: (_commands: unknown[], extras: unknown) => {
        navigations.push(extras);
        return Promise.resolve(true);
      },
    } as never,
  );
}

function createDestroyRef(): DestroyRef {
  return { destroyed: false, onDestroy: () => () => undefined } as unknown as DestroyRef;
}

function createQuery(changes: Partial<NewsQuery> = {}): NewsQuery {
  return {
    ticker: null,
    sourceCode: null,
    sentiment: null,
    tag: null,
    page: 1,
    pageSize: 30,
    sortBy: 'publishedAt',
    watchlistOnly: false,
    ...changes,
  };
}

function createResponse(
  items: NewsItem[],
  page = 1,
  totalCount = items.length,
  hasMore = false,
): PagedNewsResponse {
  return { items, page, pageSize: 30, totalCount, hasMore };
}

function createEvent(eventId: string, news: NewsItem): NewsCreatedEvent {
  return { eventId, sentAtUtc: '2026-07-28T00:00:00.000Z', news };
}

function createNews(id: number, changes: Partial<NewsItem> = {}): NewsItem {
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
    ...changes,
  };
}
