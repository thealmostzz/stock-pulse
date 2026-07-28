import { NewsItem } from './news-item';

export type NewsSortBy = 'publishedAt' | 'impact';
export type NewsSentimentFilter = NewsItem['sentiment'];

export interface NewsQuery {
  ticker: string | null;
  sourceCode: string | null;
  sentiment: NewsSentimentFilter | null;
  tag: string | null;
  page: number;
  pageSize: number;
  sortBy: NewsSortBy;
  watchlistOnly: boolean;
}

export interface PagedNewsResponse {
  items: NewsItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
}
