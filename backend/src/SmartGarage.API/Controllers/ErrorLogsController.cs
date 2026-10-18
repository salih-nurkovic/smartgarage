using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.ErrorLogs;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Recorded failures, for administrators. The only place a stack trace leaves the server.
/// </summary>
[ApiController]
[Route("api/error-logs")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class ErrorLogsController : ControllerBase
{
    private readonly IErrorLogService _errorLogs;

    public ErrorLogsController(IErrorLogService errorLogs)
    {
        _errorLogs = errorLogs;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ErrorLogListItemResponse>>> Get(
        [FromQuery] ErrorLogSearchRequest request,
        CancellationToken ct) =>
        Ok(await _errorLogs.GetAsync(request, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ErrorLogDetailResponse>> GetById(long id, CancellationToken ct) =>
        Ok(await _errorLogs.GetByIdAsync(id, ct));
}
