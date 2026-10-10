namespace SmartGarage.Domain.Exceptions;

/// <summary>
/// Base for every failure the application raises on purpose.
/// <para>
/// The message of a <see cref="DomainException"/> is written for the end user, in Bosnian, and
/// is safe to return to a client. Anything that is not a <see cref="DomainException"/> is
/// treated as unexpected: it is logged in full and the client receives only a generic message
/// and a correlation id.
/// </para>
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}