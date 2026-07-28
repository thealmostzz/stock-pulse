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

  it('emits the article and prevents scrolling when activated with Space', () => {
    const fixture = TestBed.createComponent(NewsCardComponent);
    const news = createNews(8);
    fixture.componentRef.setInput('item', news);
    let emitted: NewsItem | undefined;
    fixture.componentInstance.newsSelected.subscribe((item) => emitted = item);
    fixture.detectChanges();

    const event = new KeyboardEvent('keydown', { key: ' ', cancelable: true });
    (fixture.nativeElement.querySelector('.news-card') as HTMLElement).dispatchEvent(event);

    expect(emitted).toBe(news);
    expect(event.defaultPrevented).toBeTrue();
  });

  it('uses semantic mono typography for time, ticker, and impact score but not prose', () => {
    const fixture = TestBed.createComponent(NewsCardComponent);
    fixture.componentRef.setInput('item', createNews(9));
    fixture.detectChanges();

    const time = fixture.nativeElement.querySelector('time') as HTMLElement;
    const ticker = fixture.nativeElement.querySelector('.news-card__tickers span') as HTMLElement;
    const impactScore = fixture.nativeElement.querySelector('.news-card__impact-score') as HTMLElement | null;
    const title = fixture.nativeElement.querySelector('.news-card__title') as HTMLElement;
    const summary = fixture.nativeElement.querySelector('.news-card__summary') as HTMLElement;

    expect(time.classList.contains('sp-mono')).toBeTrue();
    expect(ticker.classList.contains('sp-mono')).toBeTrue();
    expect(impactScore?.classList.contains('sp-mono')).toBeTrue();
    expect(getComputedStyle(time).fontFamily).toContain('ui-monospace');
    expect(title.classList.contains('sp-mono')).toBeFalse();
    expect(summary.classList.contains('sp-mono')).toBeFalse();
  });

  it('exposes the selected article with aria-pressed', () => {
    const fixture = TestBed.createComponent(NewsCardComponent);
    fixture.componentRef.setInput('item', createNews(10));
    fixture.componentRef.setInput('isSelected', true);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.news-card') as HTMLElement;

    expect(card.getAttribute('aria-pressed')).toBe('true');
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
