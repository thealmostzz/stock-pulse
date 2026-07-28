using StockPulse.Contracts.News;

namespace StockPulse.Worker.Providers.Finnhub;

public sealed class FinnhubNewsClient : IProviderNewsClient
{
    public string SourceCode => "finnhub";

    public Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("Finnhub news ingestion is not implemented.");
}
