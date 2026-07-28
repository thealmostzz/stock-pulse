import { TestBed } from '@angular/core/testing';

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
});
