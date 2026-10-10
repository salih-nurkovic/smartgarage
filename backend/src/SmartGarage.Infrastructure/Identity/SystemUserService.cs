using SmartGarage.Application.Abstractions;

namespace SmartGarage.Infrastructure.Identity;

/// <summary>
/// Stands in for the current user inside the Worker, which processes messages rather than HTTP
/// requests and therefore has no signed-in person.
/// <para>
/// Audit columns written by Worker-initiated changes are left null, which reads correctly: the
/// system made the change, not a person. Scheduled cancellations and reminder dispatches are
/// exactly that.
/// </para>
/// </summary>
public sealed class SystemUserService : ICurrentUserService
{
    public int? UserId => null;

    public string? UserName => "system";

    public string? Email => null;

    public string? JwtId => null;

    public string? IpAddress => null;

    public IReadOnlyCollection<string> Roles => [];

    public bool IsAuthenticated => false;

    public bool IsInRole(string role) => false;

    public int RequireUserId() =>
        throw new InvalidOperationException(
            "The Worker has no authenticated user. Code that needs one is running in the "
            + "wrong process.");
}
