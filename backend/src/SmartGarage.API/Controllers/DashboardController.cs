using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Dtos.Dashboard;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>Headline figures for the desktop dashboard; revenue only for administrators.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct) =>
        Ok(await _dashboard.GetAsync(ct));
}
