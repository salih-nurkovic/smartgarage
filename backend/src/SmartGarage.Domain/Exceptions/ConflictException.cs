namespace SmartGarage.Domain.Exceptions;

/// <summary>The request collides with existing state, such as a taken username. Maps to 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}