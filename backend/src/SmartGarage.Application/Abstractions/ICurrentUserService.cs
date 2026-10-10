namespace SmartGarage.Application.Abstractions;

/// <summary>
/// The authenticated user of the current request, read from the JWT.
/// <para>
/// This exists so that no controller ever accepts a user id from a route or a body for an
/// operation about "me". The instructions are explicit: "userId se nikada ne prima iz rute ili
/// body-ja za operacije vezane za trenutnog korisnika; uvijek se preuzima iz JWT tokena".
/// Services ask this interface instead of parsing claims themselves.
/// </para>
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Id of the authenticated user, or null when the request is anonymous.</summary>
    int? UserId { get; }

    string? UserName { get; }

    string? Email { get; }

    /// <summary>The <c>jti</c> of the access token on this request, which identifies the session.</summary>
    string? JwtId { get; }

    /// <summary>Address the request came from, recorded on the session for auditing.</summary>
    string? IpAddress { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);

    /// <summary>
    /// The user id, or an exception when there is none. Used on paths that are already behind
    /// an authorization policy, so an anonymous caller there is a bug rather than a scenario.
    /// </summary>
    int RequireUserId();
}
