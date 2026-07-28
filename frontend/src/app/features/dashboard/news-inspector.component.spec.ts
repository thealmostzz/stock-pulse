import { TestBed } from '@angular/core/testing';

import { NewsItem } from '../../core/models/news-item';
import { NewsInspectorComponent } from './news-inspector.component';

describe('NewsInspectorComponent', () => {
  it('shows an orientation message when no news is selected', () => {
    const fixture = TestBed.createComponent(NewsInspectorComponent);
    fixture.componentRef.setInput('news', null);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('เลือกข่าวเพื่อดูรายละเอียด');
  });

  it('labels the external article link and protects the new tab', () => {
    const fixture = TestBed.createComponent(NewsInspectorComponent);
    fixture.componentRef.setInput('news', createNews());
    fixture.detectChanges();

    const externalLink = fixture.nativeElement.querySelector('a') as HTMLAnchorElement;

    expect(externalLink.textContent?.trim()).toBe('เปิดบทความต้นฉบับ');
    expect(externalLink.target).toBe('_blank');
    expect(externalLink.rel).toContain('noopener');
    expect(externalLink.rel).toContain('noreferrer');
  });

  it('uses semantic mono typography for time, ticker, and impact score but not prose', () => {
    const fixture = TestBed.createComponent(NewsInspectorComponent);
    fixture.componentRef.setInput('news', createNews());
    fixture.detectChanges();

    const time = fixture.nativeElement.querySelector('time') as HTMLElement;
    const ticker = fixture.nativeElement.querySelector('.news-inspector__tickers span') as HTMLElement;
    const impactScore = fixture.nativeElement.querySelector('.news-inspector__impact-score') as HTMLElement | null;
    const title = fixture.nativeElement.querySelector('h2') as HTMLElement;
    const summary = fixture.nativeElement.querySelector('.news-inspector__summary') as HTMLElement;

    expect(time.classList.contains('sp-mono')).toBeTrue();
    expect(ticker.classList.contains('sp-mono')).toBeTrue();
    expect(impactScore?.classList.contains('sp-mono')).toBeTrue();
    expect(title.classList.contains('sp-mono')).toBeFalse();
    expect(summary.classList.contains('sp-mono')).toBeFalse();
  });
});

function createNews(): NewsItem {
  return {
    id: 99,
    title: 'Market update',
    summary: 'A concise market update.',
    sourceCode: 'TEST',
    url: 'https://example.com/news',
    publishedAtUtc: '2026-07-28T00:00:00.000Z',
    tickers: ['SPY'],
    sentiment: 'Neutral' as const,
    impactScore: 1,
    tags: [],
  };
}
