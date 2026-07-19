using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StockPulse.Api.Controllers;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Application.Tests;

public sealed class NewsControllerTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the task specification.
    [Fact]
    public async Task Latest_MapsInvalidLimitToBadRequest()
    {
        var controller = new NewsController(NewsQueryService.CreateForTest());

        var result = await controller.Latest(201, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
        Assert.Contains("limit", details.Errors.Keys);
    }

    [Theory]
    [MemberData(nameof(InvalidQueryRequests))]
    public async Task Query_MapsInvalidRequestToBadRequest(NewsQueryRequest request)
    {
        var controller = new NewsController(NewsQueryService.CreateForTest());

        var result = await controller.Query(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
    }

    public static TheoryData<NewsQueryRequest> InvalidQueryRequests => new()
    {
        new NewsQueryRequest("invalid ticker!", null, null, null),
        new NewsQueryRequest(null, null, "mixed", null),
        new NewsQueryRequest(null, null, null, null, 0),
        new NewsQueryRequest(null, null, null, null, 1, 201),
        new NewsQueryRequest(null, null, null, null, int.MaxValue, 2)
    };
#pragma warning restore CA1707
}
