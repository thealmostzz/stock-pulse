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
