using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StockPulse.Worker.Providers.Mock;

namespace StockPulse.Worker.Tests;

public sealed class MockNewsClientTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the fixture contract.
    [Fact]
    public async Task FetchNewsAsync_ReturnsOfficialSourceUrlsInsteadOfExampleTest()
    {
        var client = new MockNewsClient(new TestHostEnvironment(FindWorkerContentRootPath()));

        var articles = await client.FetchNewsAsync(CancellationToken.None);

        Assert.Collection(
            articles,
            nvda =>
            {
                Assert.Equal("https://nvidianews.nvidia.com/news/nvidia-announces-financial-results-for-first-quarter-fiscal-2025", nvda.ExternalUrl);
                Assert.Equal("NVIDIA announces financial results for first quarter fiscal 2025", nvda.Title);
                Assert.Equal(new DateTimeOffset(2024, 5, 22, 0, 0, 0, TimeSpan.Zero), nvda.PublishedAtUtc);
                Assert.Equal(["NVDA"], nvda.Tickers);
            },
            aapl =>
            {
                Assert.Equal("https://www.apple.com/newsroom/2024/05/apple-reports-second-quarter-results/", aapl.ExternalUrl);
                Assert.Equal("Apple reports second quarter results", aapl.Title);
                Assert.Equal(new DateTimeOffset(2024, 5, 2, 0, 0, 0, TimeSpan.Zero), aapl.PublishedAtUtc);
                Assert.Equal(["AAPL"], aapl.Tickers);
            });
        Assert.All(articles, article =>
        {
            var uri = new Uri(article.ExternalUrl);
            Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
            Assert.NotEqual("example.test", uri.Host);
        });
    }
#pragma warning restore CA1707

    private static string FindWorkerContentRootPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var fixturePath = Path.Combine(directory.FullName, "src", "StockPulse.Worker", "mock-data", "news.json");
            if (File.Exists(fixturePath))
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(fixturePath)!)!;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate src/StockPulse.Worker/mock-data/news.json from the test base directory.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "StockPulse.Worker.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
