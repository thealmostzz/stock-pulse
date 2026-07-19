import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';

import { NewsItem } from '../../core/models/news-item';
import { NewsCardComponent } from './news-card.component';

@Component({
  selector: 'sp-news-feed',
  standalone: true,
  imports: [ScrollingModule, NewsCardComponent],
  template: `
    <section class="news-feed" aria-labelledby="news-feed-title">
      <header class="news-feed__header">
        <div>
          <p class="news-feed__eyebrow">LIVE INTELLIGENCE</p>
          <h1 id="news-feed-title">Market news</h1>
        </div>
        <span class="news-feed__status" aria-label="Realtime feed connected">LIVE</span>
      </header>

      @if (isLoading()) {
        <div class="news-feed__skeletons" aria-label="กำลังโหลดข่าว" aria-busy="true">
          @for (skeleton of skeletonRows; track skeleton) {
            <div class="news-feed__skeleton"></div>
          }
        </div>
      } @else if (items().length === 0) {
        <p class="news-feed__empty" role="status">เพิ่มหุ้นใน Watchlist เพื่อเริ่มติดตามข่าว</p>
      } @else {
        <cdk-virtual-scroll-viewport class="news-feed__viewport" itemSize="154" role="list" aria-label="Latest market news">
          <sp-news-card
            *cdkVirtualFor="let item of items(); let index = index; trackBy: trackByNewsId"
            [item]="item"
            [isNewest]="index === 0"
          />
        </cdk-virtual-scroll-viewport>
      }
    </section>
  `,
  styles: `
    .news-feed { height: 100dvh; display: grid; grid-template-rows: auto minmax(0, 1fr); }
    .news-feed__header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 1.4rem 1.5rem 1.1rem; border-bottom: 1px solid var(--sp-border); }
    .news-feed__eyebrow { margin: 0 0 .3rem; color: var(--sp-muted); font-size: .65rem; font-weight: 700; letter-spacing: .13em; }
    h1 { margin: 0; font-size: 1.1rem; letter-spacing: -.03em; }
    .news-feed__status { color: var(--sp-positive); font-size: .68rem; font-weight: 700; letter-spacing: .1em; }
    .news-feed__status::before { content: ''; display: inline-block; width: .45rem; height: .45rem; margin-right: .4rem; border-radius: 50%; background: currentColor; box-shadow: 0 0 .8rem currentColor; }
    .news-feed__viewport { height: 100%; }
    .news-feed__skeletons { padding: 1rem 1.5rem; }
    .news-feed__skeleton { height: 130px; margin-bottom: 12px; border: 1px solid var(--sp-border); border-radius: .55rem; background: linear-gradient(90deg, var(--sp-surface), #152533, var(--sp-surface)); background-size: 200% 100%; animation: shimmer 1.4s linear infinite; }
    .news-feed__empty { align-self: center; margin: 0; padding: 2rem; color: var(--sp-muted); text-align: center; }
    @keyframes shimmer { to { background-position: -200% 0; } }
    @media (prefers-reduced-motion: reduce) { .news-feed__skeleton { animation: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsFeedComponent {
  readonly items = input.required<NewsItem[]>();
  readonly isLoading = input(false);
  readonly skeletonRows = [1, 2, 3, 4, 5, 6];

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }
}
