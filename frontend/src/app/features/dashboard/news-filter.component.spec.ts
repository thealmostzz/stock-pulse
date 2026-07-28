import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewsQuery } from '../../core/models/news-query';
import { NewsFilterComponent } from './news-filter.component';

describe('NewsFilterComponent', () => {
  let fixture: ComponentFixture<NewsFilterComponent>;

  beforeEach(() => {
    fixture = TestBed.createComponent(NewsFilterComponent);
    fixture.componentRef.setInput('query', createQuery());
    fixture.detectChanges();
  });

  it('emits an impact sort change', () => {
    let change: Partial<NewsQuery> | undefined;
    fixture.componentInstance.queryChanged.subscribe((value: Partial<NewsQuery>) => change = value);

    const select = fixture.nativeElement.querySelector('#news-sort') as HTMLSelectElement;
    select.value = 'impact';
    select.dispatchEvent(new Event('change'));

    expect(change).toEqual({ sortBy: 'impact' });
  });

  it('renders visible labels for every filter control', () => {
    const controlIds = [
      'news-ticker',
      'news-source',
      'news-sentiment',
      'news-tag',
      'news-watchlist-only',
      'news-sort',
    ];

    for (const controlId of controlIds) {
      const control = fixture.nativeElement.querySelector(`#${controlId}`) as HTMLElement | null;
      const label = fixture.nativeElement.querySelector(`label[for="${controlId}"]`) as HTMLLabelElement | null;

      expect(control).withContext(`missing #${controlId}`).not.toBeNull();
      expect(label?.textContent?.trim()).withContext(`missing visible label for #${controlId}`).toBeTruthy();
    }
  });

  it('emits a clear request from a non-submit button', () => {
    let clearCount = 0;
    fixture.componentInstance.clearRequested.subscribe(() => clearCount += 1);

    const clearButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    clearButton.click();

    expect(clearButton.type).toBe('button');
    expect(clearCount).toBe(1);
  });
});

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
