using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Employee management. Staff can see who works in the garage and when; only an administrator
/// can hire, edit, reschedule or remove.
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employees;

    public EmployeesController(IEmployeeService employees)
    {
        _employees = employees;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeResponse>>> Get(
        [FromQuery] EmployeeSearchRequest request,
        CancellationToken ct) =>
        Ok(await _employees.GetAsync(request, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _employees.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeResponse>> Create(EmployeeCreateRequest request, CancellationToken ct)
    {
        var created = await _employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeResponse>> Update(
        int id,
        EmployeeUpdateRequest request,
        CancellationToken ct) =>
        Ok(await _employees.UpdateAsync(id, request, ct));

    [HttpPut("{id:int}/schedule")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeResponse>> SetSchedule(
        int id,
        EmployeeScheduleRequest request,
        CancellationToken ct) =>
        Ok(await _employees.SetScheduleAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<MessageResponse>> Delete(int id, CancellationToken ct) =>
        Ok(await _employees.DeleteAsync(id, ct));
}
