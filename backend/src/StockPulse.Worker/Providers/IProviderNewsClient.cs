using StockPulse.Contracts.News;

namespace StockPulse.Worker.Providers;

public interface IProviderNewsClient
{
    string SourceCode { get; }

    Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken);
}
