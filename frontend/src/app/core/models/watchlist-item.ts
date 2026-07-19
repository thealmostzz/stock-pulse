export interface WatchlistItem {
  id: number;
  ticker: string;
  displayName: string | null;
  market: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateWatchlistRequest {
  ticker: string;
  displayName: string | null;
  market: string | null;
}
