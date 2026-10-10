using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Vehicles. A customer works with their own; staff can look up any. Which vehicles a caller
/// can reach is decided in the service from the token, never from anything in the request.
/// </summary>
[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles)
    {
        _vehicles = vehicles;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<VehicleResponse>>> Get(
        [FromQuery] VehicleSearchRequest request,
        CancellationToken ct) =>
        Ok(await _vehicles.GetAsync(request, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _vehicles.GetByIdAsync(id, ct));

    // Customers only: a vehicle is created for whoever is signed in, and staff own no vehicles.
    [HttpPost]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<VehicleResponse>> Create(VehicleRequest request, CancellationToken ct)
    {
        var created = await _vehicles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> Update(int id, VehicleRequest request, CancellationToken ct) =>
        Ok(await _vehicles.UpdateAsync(id, request, ct));

    [HttpPatch("{id:int}/mileage")]
    public async Task<ActionResult<VehicleResponse>> UpdateMileage(
        int id,
        VehicleMileageRequest request,
        CancellationToken ct) =>
        Ok(await _vehicles.UpdateMileageAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<MessageResponse>> Delete(int id, CancellationToken ct) =>
        Ok(await _vehicles.DeleteAsync(id, ct));
}
