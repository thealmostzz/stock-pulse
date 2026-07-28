import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { NewsItem } from '../../core/models/news-item';

@Component({
  selector: 'sp-news-inspector',
  standalone: true,
  template: `
    @if (news(); as selectedNews) {
      <article class="news-inspector">
        <p class="news-inspector__eyebrow">CONTEXT INSPECTOR</p>
        <div class="news-inspector__meta">
          <span>{{ selectedNews.sourceCode }}</span>
          <time [attr.datetime]="selectedNews.publishedAtUtc">{{ publishedTime }}</time>
        </div>
        <h2>{{ selectedNews.title }}</h2>
        @if (selectedNews.summary) {
          <p class="news-inspector__summary">{{ selectedNews.summary }}</p>
        }
        <div class="news-inspector__tickers" aria-label="Related tickers">
          @for (ticker of selectedNews.tickers; track ticker) {
            <span>{{ ticker }}</span>
          }
        </div>
        <dl class="news-inspector__signals" aria-label="News signals">
          <div>
            <dt>Sentiment</dt>
            <dd>{{ selectedNews.sentiment }}</dd>
          </div>
          <div>
            <dt>Impact</dt>
            <dd>{{ selectedNews.impactScore }}</dd>
          </div>
        </dl>
        <a [href]="selectedNews.url" target="_blank" rel="noopener noreferrer">เปิดบทความต้นฉบับ</a>
      </article>
    } @else {
      <section class="news-inspector" aria-label="News inspector">
        <p class="news-inspector__eyebrow">CONTEXT INSPECTOR</p>
        <h2>เลือกข่าวเพื่อดูรายละเอียด</h2>
        <p class="news-inspector__summary">เปิดบทความจากรายการข่าวเพื่อดูแหล่งข้อมูลและผลกระทบต่อหุ้นที่เกี่ยวข้อง</p>
      </section>
    }
  `,
  styles: `
    :host { display: block; }
    .news-inspector { display: block; }
    .news-inspector__eyebrow { margin: 0; color: var(--sp-positive); font-size: .65rem; font-weight: 700; letter-spacing: .12em; }
    .news-inspector__meta { display: flex; gap: .6rem; margin-top: 1rem; color: var(--sp-muted); font-size: .67rem; letter-spacing: .08em; text-transform: uppercase; }
    time { margin-left: auto; letter-spacing: normal; text-transform: none; }
    h2 { margin: .55rem 0 .75rem; font-size: 1rem; line-height: 1.45; }
    .news-inspector__summary { margin: 0; color: var(--sp-muted); font-size: .81rem; line-height: 1.65; }
    .news-inspector__tickers { display: flex; flex-wrap: wrap; gap: .4rem; margin-top: 1rem; }
    .news-inspector__tickers span { border: 1px solid var(--sp-border); border-radius: .25rem; padding: .19rem .35rem; font-size: .68rem; }
    .news-inspector__signals { display: grid; grid-template-columns: repeat(2, 1fr); gap: .75rem; margin: 1.25rem 0; }
    .news-inspector__signals div { border: 1px solid var(--sp-border); border-radius: .35rem; padding: .55rem; }
    dt { color: var(--sp-muted); font-size: .65rem; letter-spacing: .08em; text-transform: uppercase; }
    dd { margin: .3rem 0 0; font-size: .8rem; font-weight: 700; }
    a { display: inline-flex; align-items: center; min-height: 2.75rem; color: var(--sp-positive); font-size: .81rem; font-weight: 700; transition: color var(--sp-motion-fast) ease; }
    a:hover { color: var(--sp-focus-ring); }
    a:focus-visible { outline: 2px solid var(--sp-focus-ring); outline-offset: 2px; }
    @media (prefers-reduced-motion: reduce) { a { transition: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsInspectorComponent {
  readonly news = input<NewsItem | null>(null);

  get publishedTime(): string {
    const selectedNews = this.news();

    return selectedNews
      ? new Intl.DateTimeFormat('th-TH', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(selectedNews.publishedAtUtc))
      : '';
  }
}
