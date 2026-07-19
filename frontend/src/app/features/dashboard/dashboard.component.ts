import { ChangeDetectionStrategy, Component, DestroyRef, Inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter, finalize, bufferTime } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { NewsApiService } from '../../core/services/news-api.service';
import { NewsHubService } from '../../core/services/news-hub.service';
import { NewsFeedComponent } from './news-feed.component';
import { WatchlistPanelComponent } from '../watchlist/watchlist-panel.component';

const maxNewsItems = 300;

@Component({
  selector: 'sp-dashboard',
  standalone: true,
  imports: [NewsFeedComponent, WatchlistPanelComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly receivedEventIds = new Set<string>();
  private readonly receivedEventIdOrder: string[] = [];

  readonly items = signal<NewsItem[]>([]);
  readonly isLoading = signal(true);

  constructor(
    @Inject(DestroyRef) private readonly destroyRef: DestroyRef | null = null,
    @Inject(NewsApiService) private readonly newsApi: NewsApiService | null = null,
    @Inject(NewsHubService) private readonly newsHub: NewsHubService | null = null,
  ) {}

  async ngOnInit(): Promise<void> {
    if (!this.destroyRef || !this.newsApi || !this.newsHub) {
      this.isLoading.set(false);
      return;
    }

    this.newsApi.getLatest(maxNewsItems)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isLoading.set(false)),
      )
      .subscribe((items) => this.items.set(items.slice(0, maxNewsItems)));

    this.newsHub.newsCreated$
      .pipe(
        bufferTime(250),
        filter((events) => events.length > 0),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((events) => this.prependEvents(events));

    await this.newsHub.connect();
  }

  prependNews(news: NewsItem): void {
    this.items.update((current) => [news, ...current].slice(0, maxNewsItems));
  }

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }

  private prependEvents(events: NewsCreatedEvent[]): void {
    const uniqueNews = events
      .filter((event) => this.rememberEventId(event.eventId))
      .map((event) => event.news);

    if (uniqueNews.length > 0) {
      this.items.update((current) => [...uniqueNews, ...current].slice(0, maxNewsItems));
    }
  }

  private rememberEventId(eventId: string): boolean {
    if (this.receivedEventIds.has(eventId)) {
      return false;
    }

    this.receivedEventIds.add(eventId);
    this.receivedEventIdOrder.push(eventId);

    if (this.receivedEventIdOrder.length > maxNewsItems) {
      const oldestEventId = this.receivedEventIdOrder.shift();
      if (oldestEventId) {
        this.receivedEventIds.delete(oldestEventId);
      }
    }

    return true;
  }
}
