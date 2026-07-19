using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController(NewsQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponseDto<NewsResponseDto>> Query([FromQuery] NewsQueryRequest request, CancellationToken cancellationToken) =>
        service.QueryAsync(request, cancellationToken);

    [HttpGet("latest")]
    public Task<IReadOnlyList<NewsResponseDto>> Latest([FromQuery] int limit, CancellationToken cancellationToken) =>
        service.GetLatestAsync(limit, cancellationToken);
}
