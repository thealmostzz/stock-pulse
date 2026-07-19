export interface NewsItem {
  id: number;
  title: string;
  summary: string | null;
  sourceCode: string;
  url: string;
  publishedAtUtc: string;
  tickers: string[];
  sentiment: 'Positive' | 'Negative' | 'Neutral';
  impactScore: number;
  tags: string[];
}

export interface NewsCreatedEvent {
  eventId: string;
  sentAtUtc: string;
  news: NewsItem;
}
