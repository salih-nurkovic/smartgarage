namespace SmartGarage.Domain.Exceptions;

/// <summary>The caller could not be authenticated: wrong credentials, revoked or expired token. Maps to 401.</summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }
}