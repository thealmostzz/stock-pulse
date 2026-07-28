import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { NewsQuery } from '../../core/models/news-query';

@Component({
  selector: 'sp-news-filter',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="news-filter" aria-label="ตัวกรองข่าว">
      <div class="news-filter__field">
        <label for="news-ticker">Ticker</label>
        <input
          id="news-ticker"
          type="text"
          autocomplete="off"
          [ngModel]="query().ticker ?? ''"
          (ngModelChange)="update({ ticker: $event })"
        />
      </div>

      <div class="news-filter__field">
        <label for="news-source">แหล่งข่าว</label>
        <input
          id="news-source"
          type="text"
          autocomplete="off"
          [ngModel]="query().sourceCode ?? ''"
          (ngModelChange)="update({ sourceCode: $event })"
        />
      </div>

      <div class="news-filter__field">
        <label for="news-sentiment">Sentiment</label>
        <select
          id="news-sentiment"
          [ngModel]="query().sentiment"
          (ngModelChange)="update({ sentiment: $event })"
        >
          <option [ngValue]="null">ทั้งหมด</option>
          <option value="Positive">Positive</option>
          <option value="Neutral">Neutral</option>
          <option value="Negative">Negative</option>
        </select>
      </div>

      <div class="news-filter__field">
        <label for="news-tag">Tag</label>
        <input
          id="news-tag"
          type="text"
          autocomplete="off"
          [ngModel]="query().tag ?? ''"
          (ngModelChange)="update({ tag: $event })"
        />
      </div>

      <div class="news-filter__field news-filter__field--checkbox">
        <input
          id="news-watchlist-only"
          type="checkbox"
          [ngModel]="query().watchlistOnly"
          (ngModelChange)="update({ watchlistOnly: $event })"
        />
        <label for="news-watchlist-only">เฉพาะ Watchlist</label>
      </div>

      <div class="news-filter__field">
        <label for="news-sort">เรียงตาม</label>
        <select
          id="news-sort"
          [ngModel]="query().sortBy"
          (ngModelChange)="update({ sortBy: $event })"
        >
          <option value="publishedAt">ข่าวล่าสุด</option>
          <option value="impact">Impact</option>
        </select>
      </div>

      <button type="button" class="news-filter__clear" (click)="clearRequested.emit()">ล้างตัวกรอง</button>
    </section>
  `,
  styles: `
    .news-filter { display: flex; flex-wrap: wrap; align-items: end; gap: .75rem; }
    .news-filter__field { display: grid; gap: .35rem; }
    .news-filter__field label { color: var(--sp-muted); font-size: .72rem; }
    .news-filter__field input, .news-filter__field select, .news-filter__clear { min-height: 2.25rem; border: 1px solid var(--sp-border); border-radius: .3rem; background: var(--sp-surface); color: var(--sp-text); padding: .45rem .6rem; font: inherit; font-size: .78rem; }
    .news-filter__field--checkbox { display: flex; align-items: center; min-height: 2.25rem; }
    .news-filter__field--checkbox input { min-height: auto; }
    .news-filter__clear { cursor: pointer; }
    input:focus-visible, select:focus-visible, button:focus-visible { outline: 2px solid var(--sp-positive); outline-offset: 2px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsFilterComponent {
  readonly query = input.required<NewsQuery>();
  readonly queryChanged = output<Partial<NewsQuery>>();
  readonly clearRequested = output<void>();

  update(change: Partial<NewsQuery>): void {
    this.queryChanged.emit(change);
  }
}
