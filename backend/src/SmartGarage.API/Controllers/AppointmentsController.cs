using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Appointments. Every action is one call into the service; whether a status change is
/// allowed is decided by the state machine there, never here.
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;

    public AppointmentsController(IAppointmentService appointments)
    {
        _appointments = appointments;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AppointmentListItemResponse>>> Get(
        [FromQuery] AppointmentSearchRequest request,
        CancellationToken ct) =>
        Ok(await _appointments.GetAsync(request, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<AppointmentSummaryResponse>> GetSummary(CancellationToken ct) =>
        Ok(await _appointments.GetSummaryAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDetailResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _appointments.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<AppointmentDetailResponse>> Create(
        AppointmentCreateRequest request,
        CancellationToken ct)
    {
        var created = await _appointments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<ActionResult<AppointmentDetailResponse>> Confirm(int id, CancellationToken ct) =>
        Ok(await _appointments.ConfirmAsync(id, ct));

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<ActionResult<AppointmentDetailResponse>> Start(int id, CancellationToken ct) =>
        Ok(await _appointments.StartAsync(id, ct));

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<ActionResult<AppointmentDetailResponse>> Complete(
        int id,
        AppointmentCompleteRequest request,
        CancellationToken ct) =>
        Ok(await _appointments.CompleteAsync(id, request, ct));

    // Open to customers and staff alike: who may cancel what, and until when, is a rule of
    // the state machine and the cut-off, both applied in the service.
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<AppointmentDetailResponse>> Cancel(
        int id,
        AppointmentCancelRequest request,
        CancellationToken ct) =>
        Ok(await _appointments.CancelAsync(id, request, ct));

    [HttpPut("{id:int}/reschedule")]
    public async Task<ActionResult<AppointmentDetailResponse>> Reschedule(
        int id,
        AppointmentRescheduleRequest request,
        CancellationToken ct) =>
        Ok(await _appointments.RescheduleAsync(id, request, ct));

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<AppointmentDetailResponse>> Assign(
        int id,
        AppointmentAssignRequest request,
        CancellationToken ct) =>
        Ok(await _appointments.AssignAsync(id, request, ct));
}
