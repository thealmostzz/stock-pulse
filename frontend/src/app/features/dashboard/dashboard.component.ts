import { ChangeDetectionStrategy, Component, computed, DestroyRef, Inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { HubConnectionState } from '@microsoft/signalr';
import { bufferTime, filter, finalize } from 'rxjs';

import { NewsCreatedEvent, NewsItem } from '../../core/models/news-item';
import { NewsQuery, NewsSentimentFilter, NewsSortBy, PagedNewsResponse } from '../../core/models/news-query';
import { NewsApiService } from '../../core/services/news-api.service';
import { NewsHubService } from '../../core/services/news-hub.service';
import { NewsFeedComponent } from './news-feed.component';
import { WatchlistPanelComponent } from '../watchlist/watchlist-panel.component';

const maxNewsItems = 300;
const maxPageOffset = 2_147_483_647;
const validTicker = /^[A-Z][A-Z0-9.-]{0,19}$/;
const validSentiments: readonly NewsSentimentFilter[] = ['Positive', 'Negative', 'Neutral'];
const defaultNewsQuery: NewsQuery = {
  ticker: null,
  sourceCode: null,
  sentiment: null,
  tag: null,
  page: 1,
  pageSize: 30,
  sortBy: 'publishedAt',
  watchlistOnly: false,
};

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
  private latestRequestId = 0;
  private activeReplacementRequestId: number | null = null;
  private pendingRealtimeItems: NewsItem[] = [];
  private hasPublishedActiveWatchlistTickers = false;

  readonly items = signal<NewsItem[]>([]);
  readonly isLoading = signal(true);
  readonly query = signal<NewsQuery>({ ...defaultNewsQuery });
  readonly totalCount = signal(0);
  readonly hasMore = signal(false);
  readonly isLoadingMore = signal(false);
  readonly errorMessage = signal('');
  readonly activeWatchlistTickers = signal<readonly string[]>([]);
  readonly connectionState = computed(() => this.newsHub?.connectionState() ?? HubConnectionState.Disconnected);

  constructor(
    @Inject(DestroyRef) private readonly destroyRef: DestroyRef | null = null,
    @Inject(NewsApiService) private readonly newsApi: NewsApiService | null = null,
    @Inject(NewsHubService) private readonly newsHub: NewsHubService | null = null,
    @Inject(ActivatedRoute) private readonly route: ActivatedRoute | null = null,
    @Inject(Router) private readonly router: Router | null = null,
  ) {}

  async ngOnInit(): Promise<void> {
    const initialQuery = this.readQueryFromUrl();
    this.query.set(initialQuery);
    this.loadQuery(initialQuery, false);

    if (!this.destroyRef || !this.newsHub) {
      return;
    }

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

  updateQuery(change: Partial<NewsQuery>): void {
    const nextQuery = this.normalizeQuery({
      ...this.query(),
      ...change,
      page: 1,
      pageSize: defaultNewsQuery.pageSize,
    });

    this.query.set(nextQuery);
    this.writeQueryToUrl(nextQuery);
    this.loadQuery(nextQuery, false);
  }

  clearQuery(): void {
    const nextQuery = { ...defaultNewsQuery };
    this.query.set(nextQuery);
    this.writeQueryToUrl(nextQuery);
    this.loadQuery(nextQuery, false);
  }

  loadNextPage(): void {
    if (!this.hasMore() || this.isLoading() || this.isLoadingMore()) {
      return;
    }

    const nextQuery = { ...this.query(), page: this.query().page + 1 };
    this.loadQuery(nextQuery, true);
  }

  setActiveWatchlistTickers(tickers: readonly string[]): void {
    const normalizedTickers = [...new Set(
      tickers
        .map((ticker) => ticker.trim().toUpperCase())
        .filter((ticker) => validTicker.test(ticker)),
    )];
    const previousTickers = this.activeWatchlistTickers();
    const tickersChanged = previousTickers.length !== normalizedTickers.length
      || normalizedTickers.some((ticker) => !previousTickers.includes(ticker));
    const shouldReload = this.hasPublishedActiveWatchlistTickers && tickersChanged && this.query().watchlistOnly;

    this.activeWatchlistTickers.set(normalizedTickers);
    this.hasPublishedActiveWatchlistTickers = true;

    if (shouldReload) {
      const nextQuery = { ...this.query(), page: 1 };
      this.query.set(nextQuery);
      this.writeQueryToUrl(nextQuery);
      this.loadQuery(nextQuery, false);
    }
  }

  prependNews(news: NewsItem): void {
    this.items.update((current) => this.mergeNews([news], current));
  }

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }

  private loadQuery(requestedQuery: NewsQuery, append: boolean): void {
    if (!this.destroyRef || !this.newsApi) {
      this.isLoading.set(false);
      this.isLoadingMore.set(false);
      return;
    }

    const requestId = ++this.latestRequestId;
    this.errorMessage.set('');

    if (append) {
      this.isLoadingMore.set(true);
    } else {
      this.isLoading.set(true);
      this.isLoadingMore.set(false);
      this.totalCount.set(0);
      this.hasMore.set(false);
      this.activeReplacementRequestId = requestId;
      this.pendingRealtimeItems = [];
      this.items.update((current) => current.filter((item) => this.matchesActiveQuery(item)));
    }

    this.newsApi.query(requestedQuery)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.finishRequest(requestId, append)),
      )
      .subscribe({
        next: (response) => this.applyResponse(requestId, requestedQuery, response, append),
        error: () => {
          if (requestId === this.latestRequestId) {
            this.errorMessage.set('ไม่สามารถโหลดข่าวได้');
          }
        },
      });
  }

  private applyResponse(
    requestId: number,
    requestedQuery: NewsQuery,
    response: PagedNewsResponse,
    append: boolean,
  ): void {
    if (requestId !== this.latestRequestId) {
      return;
    }

    if (append) {
      this.items.update((current) => this.mergeNews(current, response.items));
      this.query.set(requestedQuery);
      this.writeQueryToUrl(requestedQuery);
    } else {
      this.items.set(this.mergeNews(this.pendingRealtimeItems, response.items));
    }

    this.totalCount.set(response.totalCount);
    this.hasMore.set(response.hasMore);
  }

  private finishRequest(requestId: number, append: boolean): void {
    if (requestId !== this.latestRequestId) {
      return;
    }

    if (append) {
      this.isLoadingMore.set(false);
      return;
    }

    this.isLoading.set(false);
    this.activeReplacementRequestId = null;
    this.pendingRealtimeItems = [];
  }

  private prependEvents(events: NewsCreatedEvent[]): void {
    const uniqueNews = events
      .filter((event) => this.rememberEventId(event.eventId))
      .map((event) => event.news)
      .filter((news) => this.matchesActiveQuery(news));

    if (uniqueNews.length === 0) {
      return;
    }

    if (this.activeReplacementRequestId === this.latestRequestId) {
      this.pendingRealtimeItems = this.mergeNews(uniqueNews, this.pendingRealtimeItems);
    }

    this.items.update((current) => this.mergeNews(uniqueNews, current));
  }

  private matchesActiveQuery(news: NewsItem): boolean {
    const activeQuery = this.query();
    const normalizedNewsTickers = news.tickers.map((ticker) => ticker.trim().toUpperCase());
    const ticker = activeQuery.ticker?.trim().toUpperCase();
    const sourceCode = activeQuery.sourceCode?.trim().toLowerCase();
    const tag = activeQuery.tag?.trim();

    return (!ticker || normalizedNewsTickers.includes(ticker))
      && (!sourceCode || news.sourceCode.trim().toLowerCase() === sourceCode)
      && (!activeQuery.sentiment || news.sentiment === activeQuery.sentiment)
      && (!tag || news.tags.includes(tag))
      && (!activeQuery.watchlistOnly || this.activeWatchlistTickers().some((activeTicker) =>
        normalizedNewsTickers.includes(activeTicker)));
  }

  private mergeNews(preferredItems: readonly NewsItem[], remainingItems: readonly NewsItem[]): NewsItem[] {
    const seenNewsIds = new Set<number>();
    const mergedItems: NewsItem[] = [];

    for (const item of [...preferredItems, ...remainingItems]) {
      if (!seenNewsIds.has(item.id)) {
        seenNewsIds.add(item.id);
        mergedItems.push(item);
      }
    }

    return mergedItems
      .sort((left, right) => this.compareNews(left, right))
      .slice(0, maxNewsItems);
  }

  private compareNews(left: NewsItem, right: NewsItem): number {
    if (this.query().sortBy === 'impact' && left.impactScore !== right.impactScore) {
      return right.impactScore - left.impactScore;
    }

    const publicationOrder = Date.parse(right.publishedAtUtc) - Date.parse(left.publishedAtUtc);
    return publicationOrder || right.id - left.id;
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

  private readQueryFromUrl(): NewsQuery {
    if (!this.route) {
      return { ...defaultNewsQuery };
    }

    const params = this.route.snapshot.queryParamMap;
    const ticker = this.readTicker(params);
    const sourceCode = this.readText(params, 'sourceCode');
    const sentiment = this.readSentiment(params);
    const tag = this.readText(params, 'tag');
    const sortBy = this.readSortBy(params);
    const watchlistOnly = params.get('watchlistOnly')?.trim().toLowerCase() === 'true';
    const page = this.readPage(params);

    return { ...defaultNewsQuery, ticker, sourceCode, sentiment, tag, page, sortBy, watchlistOnly };
  }

  private readTicker(params: ParamMap): string | null {
    const ticker = this.readText(params, 'ticker');
    return ticker && validTicker.test(ticker.toUpperCase()) ? ticker : null;
  }

  private readSentiment(params: ParamMap): NewsSentimentFilter | null {
    const sentiment = this.readText(params, 'sentiment')?.toLowerCase();
    return validSentiments.find((candidate) => candidate.toLowerCase() === sentiment) ?? null;
  }

  private readSortBy(params: ParamMap): NewsSortBy {
    return params.get('sortBy')?.trim().toLowerCase() === 'impact' ? 'impact' : 'publishedAt';
  }

  private readPage(params: ParamMap): number {
    const pageValue = params.get('page')?.trim() ?? '';
    if (!/^[1-9]\d*$/.test(pageValue)) {
      return defaultNewsQuery.page;
    }

    const page = Number(pageValue);
    const offset = (page - 1) * defaultNewsQuery.pageSize;
    return Number.isSafeInteger(page) && offset <= maxPageOffset ? page : defaultNewsQuery.page;
  }

  private readText(params: ParamMap, name: string): string | null {
    const value = params.get(name)?.trim();
    return value ? value : null;
  }

  private normalizeQuery(query: NewsQuery): NewsQuery {
    return {
      ...query,
      ticker: this.trimText(query.ticker),
      sourceCode: this.trimText(query.sourceCode),
      tag: this.trimText(query.tag),
      page: 1,
      pageSize: defaultNewsQuery.pageSize,
    };
  }

  private trimText(value: string | null): string | null {
    const trimmedValue = value?.trim();
    return trimmedValue ? trimmedValue : null;
  }

  private writeQueryToUrl(query: NewsQuery): void {
    if (!this.router || !this.route) {
      return;
    }

    const queryParams: Record<string, string> = {};
    if (query.ticker) {
      queryParams['ticker'] = query.ticker;
    }
    if (query.sourceCode) {
      queryParams['sourceCode'] = query.sourceCode;
    }
    if (query.sentiment) {
      queryParams['sentiment'] = query.sentiment;
    }
    if (query.tag) {
      queryParams['tag'] = query.tag;
    }
    if (query.page !== defaultNewsQuery.page) {
      queryParams['page'] = query.page.toString();
    }
    if (query.sortBy !== defaultNewsQuery.sortBy) {
      queryParams['sortBy'] = query.sortBy;
    }
    if (query.watchlistOnly) {
      queryParams['watchlistOnly'] = 'true';
    }

    void this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
  }
}
