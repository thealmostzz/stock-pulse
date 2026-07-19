using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController(NewsQueryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponseDto<NewsResponseDto>>> Query(
        [FromQuery] NewsQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.QueryAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ValidationFailure(exception);
        }
    }

    [HttpGet("latest")]
    public async Task<ActionResult<IReadOnlyList<NewsResponseDto>>> Latest([FromQuery] int limit, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetLatestAsync(limit, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ValidationFailure(exception);
        }
    }

    private BadRequestObjectResult ValidationFailure(ArgumentException exception) =>
        BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more query parameters are invalid."
        });
}
