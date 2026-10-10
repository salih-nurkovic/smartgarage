using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>User administration for the desktop application. Administrators only.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    // The parameter must not be called "search": the binder would then treat the query key
    // "search" as a prefix for this object and leave its Search property empty.
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> Get(
        [FromQuery] UserSearchRequest request,
        CancellationToken ct) =>
        Ok(await _users.GetAsync(request, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _users.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserCreateRequest request, CancellationToken ct)
    {
        var created = await _users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserResponse>> Update(
        int id,
        UserUpdateRequest request,
        CancellationToken ct) =>
        Ok(await _users.UpdateAsync(id, request, ct));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<UserResponse>> SetStatus(
        int id,
        UserStatusRequest request,
        CancellationToken ct) =>
        Ok(await _users.SetStatusAsync(id, request, ct));
}
