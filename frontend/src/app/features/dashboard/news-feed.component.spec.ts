import { TestBed } from '@angular/core/testing';
import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { By } from '@angular/platform-browser';

import { NewsItem } from '../../core/models/news-item';
import { NewsCardComponent } from './news-card.component';
import { NewsFeedComponent } from './news-feed.component';

describe('NewsFeedComponent', () => {
  it('shows offline when no realtime connection is available', () => {
    const fixture = TestBed.createComponent(NewsFeedComponent);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('.news-feed__status') as HTMLElement;

    expect(status.textContent?.trim()).toBe('OFFLINE');
    expect(status.getAttribute('aria-label')).toBe('Realtime feed offline');
  });

  it('forwards the rendered card selection', async () => {
    const fixture = TestBed.createComponent(NewsFeedComponent);
    const news = createNews(7);
    fixture.componentRef.setInput('items', [news]);
    let emitted: NewsItem | undefined;
    fixture.componentInstance.newsSelected.subscribe((item) => emitted = item);
    fixture.detectChanges();
    await fixture.whenStable();

    const viewport = fixture.debugElement.query(By.directive(CdkVirtualScrollViewport)).componentInstance as CdkVirtualScrollViewport;
    Object.defineProperty(viewport.elementRef.nativeElement, 'clientHeight', { configurable: true, value: 154 });
    Object.defineProperty(viewport.elementRef.nativeElement, 'clientWidth', { configurable: true, value: 600 });
    viewport.checkViewportSize();
    await fixture.whenStable();
    fixture.detectChanges();

    const card = fixture.debugElement.query(By.directive(NewsCardComponent)).componentInstance as NewsCardComponent;
    card.newsSelected.emit(news);

    expect(emitted).toBe(news);
  });
});

function createNews(id: number): NewsItem {
  return {
    id,
    title: `News ${id}`,
    summary: 'A concise market update.',
    sourceCode: 'TEST',
    url: 'https://example.com/news',
    publishedAtUtc: '2026-07-28T00:00:00.000Z',
    tickers: ['SPY'],
    sentiment: 'Neutral',
    impactScore: 1,
    tags: [],
  };
}
