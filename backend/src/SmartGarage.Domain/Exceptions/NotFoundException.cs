namespace SmartGarage.Domain.Exceptions;

/// <summary>The requested record does not exist or is not visible to the caller. Maps to 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}