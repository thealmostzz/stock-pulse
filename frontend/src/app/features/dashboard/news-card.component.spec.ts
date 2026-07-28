import { TestBed } from '@angular/core/testing';

import { NewsItem } from '../../core/models/news-item';
import { NewsCardComponent } from './news-card.component';

describe('NewsCardComponent', () => {
  it('emits the article when activated with Enter', () => {
    const fixture = TestBed.createComponent(NewsCardComponent);
    const news = createNews(7);
    fixture.componentRef.setInput('item', news);
    let emitted: NewsItem | undefined;
    fixture.componentInstance.newsSelected.subscribe((item) => emitted = item);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.news-card') as HTMLElement)
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

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
