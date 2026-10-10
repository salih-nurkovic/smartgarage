using Microsoft.AspNetCore.Mvc;
using SmartGarage.API.Extensions;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>
/// The signed-in user's own profile. There is no id in any route here: who "me" is comes from
/// the token.
/// </summary>
[ApiController]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile)
    {
        _profile = profile;
    }

    [HttpGet]
    public async Task<ActionResult<UserResponse>> Get(CancellationToken ct) =>
        Ok(await _profile.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<UserResponse>> Update(ProfileUpdateRequest request, CancellationToken ct) =>
        Ok(await _profile.UpdateAsync(request, ct));

    [HttpGet("image")]
    public async Task<IActionResult> GetImage(CancellationToken ct)
    {
        var image = await _profile.GetImageAsync(ct);
        return File(image.Content, image.ContentType);
    }

    [HttpPost("image")]
    public async Task<ActionResult<UserResponse>> SetImage(IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return Ok(await _profile.SetImageAsync(file.ToUpload(content), ct));
    }
}
