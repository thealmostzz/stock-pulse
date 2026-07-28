import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { HubConnectionState } from '@microsoft/signalr';

import { NewsItem } from '../../core/models/news-item';
import { NewsQuery } from '../../core/models/news-query';
import { NewsCardComponent } from './news-card.component';
import { NewsFilterComponent } from './news-filter.component';

@Component({
  selector: 'sp-news-feed',
  standalone: true,
  imports: [ScrollingModule, NewsCardComponent, NewsFilterComponent],
  template: `
    <section class="news-feed" aria-labelledby="news-feed-title">
      <header class="news-feed__header">
        <div class="news-feed__heading">
          <div>
            <p class="news-feed__eyebrow">LIVE INTELLIGENCE</p>
            <h1 id="news-feed-title">Market news</h1>
          </div>
          <span [class]="connectionStatusClass()" [attr.aria-label]="connectionStatusAriaLabel()">{{ connectionStatusLabel() }}</span>
        </div>
        <sp-news-filter
          [query]="query()"
          (queryChanged)="queryChanged.emit($event)"
          (clearRequested)="clearRequested.emit()"
        />
        <p class="news-feed__count" role="status" aria-live="polite" aria-atomic="true">
          แสดง {{ items().length }} จาก {{ announcedTotalCount() }} ข่าว
        </p>
      </header>

      @if (errorMessage()) {
        <p class="news-feed__error" role="alert">{{ errorMessage() }}</p>
      }

      <div class="news-feed__content">
        @if (isLoading()) {
          <div class="news-feed__skeletons" aria-label="กำลังโหลดข่าว" aria-busy="true">
            @for (skeleton of skeletonRows; track skeleton) {
              <div class="news-feed__skeleton"></div>
            }
          </div>
        } @else if (items().length === 0) {
          <p class="news-feed__empty" role="status">
            @if (hasActiveFilters()) {
              ไม่พบข่าวที่ตรงกับตัวกรองที่เลือก
            } @else {
              เพิ่มหุ้นใน Watchlist เพื่อเริ่มติดตามข่าว
            }
          </p>
        } @else {
          <cdk-virtual-scroll-viewport
            class="news-feed__viewport"
            itemSize="154"
            role="list"
            aria-label="Latest market news"
            (scrolledIndexChange)="onScrolledIndexChange($event)"
          >
            <sp-news-card
            *cdkVirtualFor="let item of items(); let index = index; trackBy: trackByNewsId"
            [item]="item"
            [isNewest]="index === 0"
            [isSelected]="item.id === selectedNewsId()"
            (newsSelected)="newsSelected.emit($event)"
          />
          </cdk-virtual-scroll-viewport>
        }
      </div>

      @if (isLoadingMore()) {
        <p class="news-feed__loading-more" role="status" aria-live="polite" aria-busy="true">กำลังโหลดข่าวเพิ่มเติม</p>
      }
    </section>
  `,
  styles: `
    .news-feed { height: 100dvh; display: flex; flex-direction: column; }
    .news-feed__header { display: grid; gap: 1rem; padding: 1.4rem 1.5rem 1.1rem; border-bottom: 1px solid var(--sp-border); }
    .news-feed__heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .news-feed__eyebrow { margin: 0 0 .3rem; color: var(--sp-muted); font-size: .65rem; font-weight: 700; letter-spacing: .13em; }
    h1 { margin: 0; font-size: 1.1rem; letter-spacing: -.03em; }
    .news-feed__status { color: var(--sp-muted); font-size: .68rem; font-weight: 700; letter-spacing: .1em; }
    .news-feed__status--connected { color: var(--sp-positive); }
    .news-feed__status--connecting, .news-feed__status--reconnecting { color: var(--sp-warning); }
    .news-feed__status::before { content: ''; display: inline-block; width: .45rem; height: .45rem; margin-right: .4rem; border-radius: 50%; background: currentColor; box-shadow: 0 0 .8rem currentColor; }
    .news-feed__count { margin: 0; color: var(--sp-muted); font-size: .72rem; }
    .news-feed__error { margin: 0; padding: .65rem 1.5rem; border-bottom: 1px solid var(--sp-border); color: var(--sp-negative); font-size: .75rem; }
    .news-feed__content { min-height: 0; flex: 1; display: grid; }
    .news-feed__viewport { height: 100%; min-height: 0; }
    .news-feed__skeletons { overflow: hidden; padding: 1rem 1.5rem; }
    .news-feed__skeleton { height: 130px; margin-bottom: 12px; border: 1px solid var(--sp-border); border-radius: .55rem; background: linear-gradient(90deg, var(--sp-surface), #152533, var(--sp-surface)); background-size: 200% 100%; animation: shimmer 1.4s linear infinite; }
    .news-feed__empty { align-self: center; margin: 0; padding: 2rem; color: var(--sp-muted); text-align: center; }
    .news-feed__loading-more { margin: 0; padding: .6rem 1.5rem; border-top: 1px solid var(--sp-border); color: var(--sp-muted); font-size: .72rem; text-align: center; }
    @keyframes shimmer { to { background-position: -200% 0; } }
    @media (prefers-reduced-motion: reduce) { .news-feed__skeleton { animation: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsFeedComponent {
  readonly items = input.required<NewsItem[]>();
  readonly selectedNewsId = input<number | null>(null);
  readonly query = input.required<NewsQuery>();
  readonly isLoading = input(false);
  readonly connectionState = input(HubConnectionState.Disconnected);
  readonly totalCount = input(0);
  readonly hasMore = input(false);
  readonly isLoadingMore = input(false);
  readonly errorMessage = input('');
  readonly queryChanged = output<Partial<NewsQuery>>();
  readonly clearRequested = output<void>();
  readonly loadMore = output<void>();
  readonly newsSelected = output<NewsItem>();
  readonly skeletonRows = [1, 2, 3, 4, 5, 6];
  readonly announcedTotalCount = computed(() => Math.max(this.totalCount(), this.items().length));
  readonly hasActiveFilters = computed(() => {
    const currentQuery = this.query();
    return Boolean(
      currentQuery.ticker
      || currentQuery.sourceCode
      || currentQuery.sentiment
      || currentQuery.tag
      || currentQuery.watchlistOnly,
    );
  });
  readonly connectionStatusLabel = computed(() => this.connectionStatus().label);
  readonly connectionStatusAriaLabel = computed(() => `Realtime feed ${this.connectionStatus().ariaLabel}`);
  readonly connectionStatusClass = computed(() => `news-feed__status news-feed__status--${this.connectionStatus().modifier}`);

  onScrolledIndexChange(index: number): void {
    if (this.hasMore() && !this.isLoadingMore() && index >= Math.max(0, this.items().length - 5)) {
      this.loadMore.emit();
    }
  }

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }

  private readonly connectionStatus = computed(() => {
    switch (this.connectionState()) {
      case HubConnectionState.Connected:
        return { label: 'LIVE', ariaLabel: 'connected', modifier: 'connected' };
      case HubConnectionState.Connecting:
        return { label: 'CONNECTING', ariaLabel: 'connecting', modifier: 'connecting' };
      case HubConnectionState.Reconnecting:
        return { label: 'RECONNECTING', ariaLabel: 'reconnecting', modifier: 'reconnecting' };
      default:
        return { label: 'OFFLINE', ariaLabel: 'offline', modifier: 'offline' };
    }
  });
}
