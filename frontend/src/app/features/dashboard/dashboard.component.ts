import { ChangeDetectionStrategy, Component, computed, DestroyRef, ElementRef, HostListener, Inject, OnInit, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnectionState } from '@microsoft/signalr';
import { filter, finalize, bufferTime } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { NewsApiService } from '../../core/services/news-api.service';
import { NewsHubService } from '../../core/services/news-hub.service';
import { NewsFeedComponent } from './news-feed.component';
import { NewsInspectorComponent } from './news-inspector.component';
import { WatchlistPanelComponent } from '../watchlist/watchlist-panel.component';

const maxNewsItems = 300;
const initialNewsLimit = 30;
const desktopLayoutMinWidth = 720;

@Component({
  selector: 'sp-dashboard',
  standalone: true,
  imports: [NewsFeedComponent, NewsInspectorComponent, WatchlistPanelComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly receivedEventIds = new Set<string>();
  private readonly receivedEventIdOrder: string[] = [];

  @ViewChild('watchlistDisclosure') private watchlistDisclosure?: ElementRef<HTMLDetailsElement>;

  readonly items = signal<NewsItem[]>([]);
  readonly selectedNews = signal<NewsItem | null>(null);
  readonly isLoading = signal(true);
  readonly connectionState = computed(() => this.newsHub?.connectionState() ?? HubConnectionState.Disconnected);

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

    this.newsApi.getLatest(initialNewsLimit)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isLoading.set(false)),
      )
      .subscribe((items) => this.mergeInitialItems(items));

    this.newsHub.newsCreated$
      .pipe(
        bufferTime(250),
        filter((events) => events.length > 0),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((events) => this.prependEvents(events));

    try {
      await this.newsHub.connect();
    } catch {
      // NewsHubService exposes the disconnected state; keep the HTTP feed usable.
    }
  }

  prependNews(news: NewsItem): void {
    this.items.update((current) => [news, ...current].slice(0, maxNewsItems));
  }

  selectNews(news: NewsItem): void {
    this.selectedNews.set(news);
  }

  @HostListener('window:resize')
  ensureWatchlistVisibleOnDesktop(): void {
    const disclosure = this.watchlistDisclosure?.nativeElement;

    if (window.innerWidth >= desktopLayoutMinWidth && disclosure && !disclosure.open) {
      disclosure.open = true;
    }
  }

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }

  private prependEvents(events: NewsCreatedEvent[]): void {
    const uniqueNews = events
      .filter((event) => this.rememberEventId(event.eventId))
      .map((event) => event.news);

    if (uniqueNews.length > 0) {
      this.items.update((current) => this.mergeNews(uniqueNews, current));
    }
  }

  private mergeInitialItems(initialItems: NewsItem[]): void {
    this.items.update((current) => this.mergeNews(current, initialItems));
  }

  private mergeNews(preferredItems: NewsItem[], remainingItems: NewsItem[]): NewsItem[] {
    const seenNewsIds = new Set<number>();
    const mergedItems: NewsItem[] = [];

    if (this.appendUniqueItems(preferredItems, seenNewsIds, mergedItems)) {
      return mergedItems;
    }

    this.appendUniqueItems(remainingItems, seenNewsIds, mergedItems);
    return mergedItems;
  }

  private appendUniqueItems(items: NewsItem[], seenNewsIds: Set<number>, mergedItems: NewsItem[]): boolean {
    for (const item of items) {
      if (!seenNewsIds.has(item.id)) {
        seenNewsIds.add(item.id);
        mergedItems.push(item);
      }

      if (mergedItems.length === maxNewsItems) {
        return true;
      }
    }

    return false;
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
