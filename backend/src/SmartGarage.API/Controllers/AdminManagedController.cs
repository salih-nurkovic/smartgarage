using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// The five endpoints of a list that any signed-in user may read and only an administrator
/// may change: the reference tables and the service catalogue.
/// <para>
/// A concrete controller adds a route and nothing else, so every such list behaves the same
/// way and the administrator policy cannot be left off one of them by oversight.
/// </para>
/// </summary>
[ApiController]
public abstract class AdminManagedController<TResponse, TSearch, TRequest> : ControllerBase
{
    private readonly ICrudService<TResponse, TSearch, TRequest> _service;

    protected AdminManagedController(ICrudService<TResponse, TSearch, TRequest> service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TResponse>>> Get(
        [FromQuery] TSearch request,
        CancellationToken ct) =>
        Ok(await _service.GetAsync(request, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<TResponse>> Create(TRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<TResponse>> Update(int id, TRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<MessageResponse>> Delete(int id, CancellationToken ct) =>
        Ok(await _service.DeleteAsync(id, ct));
}
