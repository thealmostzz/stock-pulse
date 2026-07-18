using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
public sealed class WatchlistController(WatchlistService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WatchlistItemDto>> GetAll(CancellationToken cancellationToken) =>
        service.GetAllAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<WatchlistItemDto>> Add(CreateWatchlistRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var item = await service.AddAsync(request, cancellationToken);
            return Created($"api/watchlist/{item.Ticker}", item);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Conflict(new ProblemDetails { Title = "Ticker already exists.", Status = StatusCodes.Status409Conflict });
        }
    }

    [HttpDelete("{ticker}")]
    public async Task<IActionResult> Remove(string ticker, CancellationToken cancellationToken)
    {
        try
        {
            return await service.RemoveAsync(ticker, cancellationToken) ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
