namespace SmartGarage.Domain.Exceptions;

/// <summary>
/// A service the system depends on, such as the payment provider, failed or refused the
/// request. Maps to 502: the request itself was fine, the problem is upstream.
/// </summary>
public sealed class ExternalServiceException : DomainException
{
    public ExternalServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
