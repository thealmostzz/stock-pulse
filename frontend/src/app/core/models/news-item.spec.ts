import { NewsItem } from './news-item';

describe('NewsItem', () => {
  it('accepts the PascalCase sentiment returned by the API', () => {
    const item: NewsItem = {
      id: 1,
      title: 'NVIDIA earnings',
      summary: null,
      sourceCode: 'test',
      url: 'https://example.test/news',
      publishedAtUtc: '2026-07-19T00:00:00Z',
      tickers: ['NVDA'],
      sentiment: 'Neutral',
      impactScore: 0,
      tags: [],
    };

    expect(item.sentiment).toBe('Neutral');
  });
});
