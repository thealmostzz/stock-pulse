using System.Text.Json;
using StockPulse.Contracts.News;

namespace StockPulse.Worker.Providers.Mock;

public sealed class MockNewsClient(IHostEnvironment environment) : IProviderNewsClient
{
    private const string FixturePath = "mock-data/news.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public string SourceCode => "mock";

    public async Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, FixturePath);
        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<List<MockNewsItem>>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (fixture is null)
        {
            return [];
        }

        return fixture
            .Select(item => new NormalizedNewsDto(
                SourceCode,
                item.Id,
                item.Url,
                item.Title,
                item.Summary,
                item.PublishedAtUtc,
                item.Tickers.AsReadOnly(),
                JsonSerializer.SerializeToDocument(item)))
            .ToList()
            .AsReadOnly();
    }

    private sealed class MockNewsItem
    {
        public string Id { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Summary { get; init; }
        public DateTimeOffset PublishedAtUtc { get; init; }
        public List<string> Tickers { get; init; } = [];
    }
}
