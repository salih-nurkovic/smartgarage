using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Reviews;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>Reviews: reading them, writing one for a completed appointment, and moderation.</summary>
[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews)
    {
        _reviews = reviews;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ReviewResponse>>> Get(
        [FromQuery] ReviewSearchRequest request,
        CancellationToken ct) =>
        Ok(await _reviews.GetAsync(request, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<ReviewSummaryResponse>> GetSummary(
        [FromQuery] ReviewSummaryRequest request,
        CancellationToken ct) =>
        Ok(await _reviews.GetSummaryAsync(request, ct));

    [HttpGet("reviewable")]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<IReadOnlyList<ReviewableAppointmentResponse>>> GetReviewable(CancellationToken ct) =>
        Ok(await _reviews.GetReviewableAsync(ct));

    [HttpPost]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<ReviewResponse>> Create(ReviewCreateRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _reviews.CreateAsync(request, ct));

    [HttpPatch("{id:int}/visibility")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ReviewResponse>> SetVisibility(
        int id,
        ReviewVisibilityRequest request,
        CancellationToken ct) =>
        Ok(await _reviews.SetVisibilityAsync(id, request, ct));

    // Whose review may be deleted is decided in the service: the author's own, or any for an
    // administrator.
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<MessageResponse>> Delete(int id, CancellationToken ct) =>
        Ok(await _reviews.DeleteAsync(id, ct));
}
