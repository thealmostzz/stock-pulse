import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { WatchlistItem } from '../../core/models/watchlist-item';
import { WatchlistApiService } from '../../core/services/watchlist-api.service';

const validTicker = /^[A-Z0-9./-]{1,16}$/;

@Component({
  selector: 'sp-watchlist-panel',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="watchlist" aria-labelledby="watchlist-title">
      <header><p>YOUR MARKET</p><h2 id="watchlist-title">Watchlist</h2></header>
      <form class="watchlist__form" (ngSubmit)="addTicker()">
        <label for="watchlist-ticker">เพิ่มหุ้น</label>
        <div>
          <input id="watchlist-ticker" name="ticker" [(ngModel)]="ticker" maxlength="16" autocomplete="off" placeholder="เช่น NVDA" />
          <button type="submit" [disabled]="isSaving()">+</button>
        </div>
      </form>
      @if (errorMessage()) { <p class="watchlist__error" role="alert">{{ errorMessage() }}</p> }
      <ul aria-live="polite">
        @for (item of items(); track item.id) {
          <li>
            <span><strong>{{ item.ticker }}</strong><small>{{ item.displayName || item.market || 'ติดตามข่าวล่าสุด' }}</small></span>
            <button type="button" (click)="removeTicker(item)" [attr.aria-label]="'ลบ ' + item.ticker">×</button>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .watchlist { padding: 1.4rem 1rem; } header p { margin: 0 0 .35rem; color: var(--sp-muted); font-size: .63rem; font-weight: 700; letter-spacing: .12em; } h2 { margin: 0; font-size: 1rem; } .watchlist__form { margin: 1.6rem 0 1rem; } label { display: block; margin-bottom: .4rem; color: var(--sp-muted); font-size: .72rem; } .watchlist__form div { display: flex; } input { min-width: 0; width: 100%; border: 1px solid var(--sp-border); border-radius: .3rem 0 0 .3rem; background: var(--sp-bg); color: var(--sp-text); padding: .55rem .6rem; font: inherit; font-size: .78rem; } button { border: 1px solid var(--sp-border); background: var(--sp-surface); color: var(--sp-text); cursor: pointer; } .watchlist__form button { width: 2.2rem; border-left: 0; border-radius: 0 .3rem .3rem 0; font-size: 1.05rem; } button:focus-visible, input:focus-visible { outline: 2px solid var(--sp-positive); outline-offset: 2px; } button:disabled { cursor: wait; opacity: .55; } ul { display: grid; gap: .3rem; margin: 0; padding: 0; list-style: none; } li { display: flex; align-items: center; justify-content: space-between; padding: .65rem .1rem; border-bottom: 1px solid var(--sp-border); } strong, small { display: block; } strong { font-size: .78rem; } small { margin-top: .2rem; color: var(--sp-muted); font-size: .66rem; } li button { width: 1.55rem; height: 1.55rem; border-radius: .25rem; color: var(--sp-muted); } li button:hover { color: var(--sp-negative); } .watchlist__error { color: var(--sp-negative); font-size: .72rem; line-height: 1.4; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WatchlistPanelComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly watchlistApi = inject(WatchlistApiService);

  readonly items = signal<WatchlistItem[]>([]);
  readonly isSaving = signal(false);
  readonly errorMessage = signal('');
  ticker = '';

  ngOnInit(): void {
    this.watchlistApi.getAll().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (items) => this.items.set(items.filter((item) => item.isActive)),
      error: () => this.errorMessage.set('ไม่สามารถโหลด Watchlist ได้'),
    });
  }

  addTicker(): void {
    const normalizedTicker = this.ticker.trim().toUpperCase();
    if (!validTicker.test(normalizedTicker)) {
      this.errorMessage.set('Ticker ต้องมี A-Z, 0-9, จุด, / หรือ - และยาวไม่เกิน 16 ตัวอักษร');
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set('');
    this.watchlistApi.add({ ticker: normalizedTicker, displayName: null, market: null })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: (item) => {
          this.items.update((items) => [...items.filter((current) => current.ticker !== item.ticker), item]);
          this.ticker = '';
        },
        error: () => this.errorMessage.set('ไม่สามารถเพิ่มหุ้นใน Watchlist ได้'),
      });
  }

  removeTicker(item: WatchlistItem): void {
    this.watchlistApi.remove(item.ticker).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.items.update((items) => items.filter((current) => current.id !== item.id)),
      error: () => this.errorMessage.set('ไม่สามารถลบหุ้นจาก Watchlist ได้'),
    });
  }
}
