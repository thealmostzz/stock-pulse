import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { NewsItem } from '../../core/models/news-item';

@Component({
  selector: 'sp-news-card',
  standalone: true,
  template: `
    <article class="news-card" [class.news-card--new]="isNewest()" role="listitem" tabindex="0">
      <div class="news-card__meta">
        <span>{{ item().sourceCode }}</span>
        <time [attr.datetime]="item().publishedAtUtc">{{ publishedTime }}</time>
      </div>
      <a class="news-card__title" [href]="item().url" target="_blank" rel="noopener noreferrer">{{ item().title }}</a>
      @if (item().summary) {
        <p class="news-card__summary">{{ item().summary }}</p>
      }
      <footer class="news-card__footer">
        <div class="news-card__tickers" aria-label="Related tickers">
          @for (ticker of item().tickers; track ticker) {
            <span>{{ ticker }}</span>
          }
        </div>
        <div class="news-card__signals" aria-label="News signals">
          <span [class]="sentimentClass">{{ item().sentiment }}</span>
          <span>Impact {{ item().impactScore }}</span>
        </div>
      </footer>
    </article>
  `,
  styles: `
    .news-card { height: 154px; overflow: hidden; padding: 1rem 1.5rem; border-bottom: 1px solid var(--sp-border); outline: none; }
    .news-card:focus-visible { outline: 2px solid var(--sp-positive); outline-offset: -2px; }
    .news-card--new { animation: new-news 1.2s ease-out; }
    .news-card__meta, .news-card__footer, .news-card__signals, .news-card__tickers { display: flex; align-items: center; gap: .6rem; }
    .news-card__meta { color: var(--sp-muted); font-size: .67rem; letter-spacing: .08em; text-transform: uppercase; }
    time { margin-left: auto; letter-spacing: normal; text-transform: none; }
    .news-card__title { display: block; margin: .45rem 0; color: var(--sp-text); font-size: .95rem; font-weight: 700; line-height: 1.4; text-decoration: none; }
    .news-card__title:hover { color: var(--sp-positive); }
    .news-card__summary { display: -webkit-box; overflow: hidden; margin: 0 0 .75rem; color: var(--sp-muted); font-size: .78rem; line-height: 1.45; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
    .news-card__footer { justify-content: space-between; font-size: .68rem; }
    .news-card__tickers span, .news-card__signals span { border: 1px solid var(--sp-border); border-radius: .25rem; padding: .19rem .35rem; }
    .news-card__tickers span { color: var(--sp-text); background: var(--sp-surface); }
    .news-card__signals { color: var(--sp-muted); }
    .news-card__signals .positive { color: var(--sp-positive); border-color: color-mix(in srgb, var(--sp-positive) 55%, var(--sp-border)); }
    .news-card__signals .negative { color: var(--sp-negative); border-color: color-mix(in srgb, var(--sp-negative) 55%, var(--sp-border)); }
    .news-card__signals .neutral { color: var(--sp-warning); }
    @keyframes new-news { from { background: color-mix(in srgb, var(--sp-positive) 13%, transparent); } to { background: transparent; } }
    @media (prefers-reduced-motion: reduce) { .news-card--new { animation: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsCardComponent {
  readonly item = input.required<NewsItem>();
  readonly isNewest = input(false);

  get publishedTime(): string {
    return new Intl.DateTimeFormat('th-TH', { hour: '2-digit', minute: '2-digit' }).format(new Date(this.item().publishedAtUtc));
  }

  get sentimentClass(): string {
    return this.item().sentiment.toLowerCase();
  }
}
