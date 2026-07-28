import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { NewsItem } from '../../core/models/news-item';
import { NewsQuery } from '../../core/models/news-query';
import { NewsFeedComponent } from './news-feed.component';
import { NewsFilterComponent } from './news-filter.component';

describe('NewsFeedComponent', () => {
  it('shows offline when no realtime connection is available', () => {
    const fixture = createFixture();

    const status = fixture.nativeElement.querySelector('.news-feed__status') as HTMLElement;

    expect(status.textContent?.trim()).toBe('OFFLINE');
    expect(status.getAttribute('aria-label')).toBe('Realtime feed offline');
  });

  it('emits loadMore when virtual scrolling reaches five rows from the end', () => {
    const fixture = createFixture(createItems(10));
    let loadMoreCount = 0;
    fixture.componentInstance.loadMore.subscribe(() => loadMoreCount += 1);
    fixture.componentRef.setInput('hasMore', true);
    fixture.detectChanges();

    fixture.componentInstance.onScrolledIndexChange(4);
    fixture.componentInstance.onScrolledIndexChange(5);

    expect(loadMoreCount).toBe(1);
  });

  it('does not emit loadMore without another page or while a page is loading', () => {
    const fixture = createFixture(createItems(10));
    let loadMoreCount = 0;
    fixture.componentInstance.loadMore.subscribe(() => loadMoreCount += 1);

    fixture.componentRef.setInput('hasMore', false);
    fixture.componentInstance.onScrolledIndexChange(9);
    fixture.componentRef.setInput('hasMore', true);
    fixture.componentRef.setInput('isLoadingMore', true);
    fixture.componentInstance.onScrolledIndexChange(9);

    expect(loadMoreCount).toBe(0);
  });

  it('announces the result count, query error, and loading-more state accessibly', () => {
    const fixture = createFixture(createItems(2));
    fixture.componentRef.setInput('totalCount', 12);
    fixture.componentRef.setInput('errorMessage', 'โหลดข่าวไม่สำเร็จ');
    fixture.componentRef.setInput('isLoadingMore', true);
    fixture.detectChanges();

    const count = fixture.nativeElement.querySelector('.news-feed__count') as HTMLElement;
    const error = fixture.nativeElement.querySelector('.news-feed__error') as HTMLElement;
    const loadingMore = fixture.nativeElement.querySelector('.news-feed__loading-more') as HTMLElement;

    expect(count.textContent).toContain('2');
    expect(count.textContent).toContain('12');
    expect(count.getAttribute('aria-live')).toBe('polite');
    expect(error.getAttribute('role')).toBe('alert');
    expect(loadingMore.getAttribute('role')).toBe('status');
    expect(loadingMore.getAttribute('aria-busy')).toBe('true');
  });

  it('never announces a total smaller than the displayed item count', () => {
    const fixture = createFixture(createItems(2));
    fixture.componentRef.setInput('totalCount', 1);
    fixture.detectChanges();

    const count = fixture.nativeElement.querySelector('.news-feed__count') as HTMLElement;

    expect(count.textContent).toContain('แสดง 2 จาก 2 ข่าว');
    expect(count.textContent).not.toContain('แสดง 2 จาก 1 ข่าว');
  });

  it('forwards filter changes and clear requests to the dashboard boundary', () => {
    const fixture = createFixture();
    const filter = fixture.debugElement.query(By.directive(NewsFilterComponent)).componentInstance as NewsFilterComponent;
    let queryChange: Partial<NewsQuery> | undefined;
    let clearCount = 0;
    fixture.componentInstance.queryChanged.subscribe((change) => queryChange = change);
    fixture.componentInstance.clearRequested.subscribe(() => clearCount += 1);

    filter.update({ ticker: 'AAPL' });
    filter.clearRequested.emit();

    expect(queryChange).toEqual({ ticker: 'AAPL' });
    expect(clearCount).toBe(1);
  });
});

function createFixture(items: NewsItem[] = []) {
  const fixture = TestBed.createComponent(NewsFeedComponent);
  fixture.componentRef.setInput('items', items);
  fixture.componentRef.setInput('query', createQuery());
  fixture.detectChanges();
  return fixture;
}

function createItems(count: number): NewsItem[] {
  return Array.from({ length: count }, (_, index) => ({
    id: index + 1,
    title: `News ${index + 1}`,
    summary: null,
    sourceCode: 'TEST',
    url: 'https://example.com/news',
    publishedAtUtc: '2026-07-28T00:00:00.000Z',
    tickers: ['SPY'],
    sentiment: 'Neutral',
    impactScore: 1,
    tags: [],
  }));
}

function createQuery(): NewsQuery {
  return {
    ticker: null,
    sourceCode: null,
    sentiment: null,
    tag: null,
    page: 1,
    pageSize: 30,
    sortBy: 'publishedAt',
    watchlistOnly: false,
  };
}
