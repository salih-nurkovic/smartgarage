using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Registration, sign-in and password recovery.
/// <para>
/// These are the only anonymous endpoints in the API besides the health checks. Each one is
/// something a person must be able to do precisely because they are not signed in: create an
/// account, prove who they are, renew a session whose access token has expired, or recover a
/// forgotten password. Credentials always travel in the POST body, never in the URL.
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _auth.RegisterAsync(request, ct));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await _auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);

        return Ok(new MessageResponse(
            "Ako nalog postoji, na njegovu e-mail adresu je poslan kod za promjenu lozinke."));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<MessageResponse>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);

        return Ok(new MessageResponse(
            "Lozinka je uspješno promijenjena. Prijavite se novom lozinkom."));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<MessageResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);

        return Ok(new MessageResponse("Lozinka je uspješno promijenjena."));
    }
}
