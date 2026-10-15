using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Recommendations;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>Personal service recommendations for one of the customer's vehicles, each with its reasons.</summary>
[ApiController]
[Route("api/recommendations")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendations;

    public RecommendationsController(IRecommendationService recommendations)
    {
        _recommendations = recommendations;
    }

    [HttpGet]
    public async Task<ActionResult<RecommendationsResponse>> Get(
        [FromQuery] RecommendationRequest request,
        CancellationToken ct) =>
        Ok(await _recommendations.GetAsync(request, ct));

    [HttpPost("{serviceId:int}/clicked")]
    public async Task<ActionResult<MessageResponse>> Clicked(
        int serviceId,
        RecommendationClickRequest request,
        CancellationToken ct) =>
        Ok(await _recommendations.RecordClickAsync(serviceId, request, ct));

    [HttpPost("{serviceId:int}/done")]
    public async Task<ActionResult<MessageResponse>> Done(
        int serviceId,
        RecommendationDoneRequest request,
        CancellationToken ct) =>
        Ok(await _recommendations.MarkDoneAsync(serviceId, request, ct));
}
