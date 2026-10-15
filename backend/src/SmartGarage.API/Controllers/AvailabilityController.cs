using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>Which hours of a day can still be booked, computed on the server.</summary>
[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;

    public AvailabilityController(IAvailabilityService availability)
    {
        _availability = availability;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<SlotResponse>>> GetSlots(
        [FromQuery] SlotSearchRequest request,
        CancellationToken ct) =>
        Ok(await _availability.GetSlotsAsync(request, ct));
}
