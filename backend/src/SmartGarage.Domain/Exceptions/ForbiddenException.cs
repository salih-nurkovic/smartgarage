namespace SmartGarage.Domain.Exceptions;

/// <summary>The caller is authenticated but not allowed to do this. Maps to 403.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}