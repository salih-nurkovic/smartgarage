using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartGarage.API.Hubs;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Extensions;

/// <summary>
/// Adds the one check a signature cannot make: whether the session behind a token still exists.
/// <para>
/// By the time <see cref="TokenValidated"/> runs, the middleware has already verified the
/// signature, issuer, audience and lifetime. A token can pass all four and still belong to a
/// user who logged out a second ago, so the session is looked up before the request proceeds.
/// </para>
/// </summary>
public sealed class SessionJwtBearerEvents : JwtBearerEvents
{
    private readonly ISessionService _sessions;

    public SessionJwtBearerEvents(ISessionService sessions)
    {
        _sessions = sessions;
    }

    // A browser or mobile WebSocket cannot set an Authorization header, so SignalR sends the
    // token in the query string. It is accepted there for the hub path only: on any other
    // route a token in the URL would end up in logs and history.
    public override Task MessageReceived(MessageReceivedContext context)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments(NotificationHub.Path, StringComparison.Ordinal)
            && context.Request.Query.TryGetValue("access_token", out var token))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var jwtId = context.Principal?.FindFirstValue(AppClaims.JwtId);

        if (jwtId is null
            || !await _sessions.IsActiveAsync(jwtId, context.HttpContext.RequestAborted))
        {
            context.Fail("The session this token belongs to is no longer active.");
        }
    }

    public override Task Challenge(JwtBearerChallengeContext context)
    {
        // Replaces the default empty 401 with the standard error body.
        context.HandleResponse();

        return ApiErrorWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Niste prijavljeni ili je vaša sesija istekla. Prijavite se ponovo.");
    }

    public override Task Forbidden(ForbiddenContext context) =>
        ApiErrorWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Nemate dozvolu za ovu radnju.");
}
