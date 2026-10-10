namespace SmartGarage.Domain.Exceptions;

/// <summary>The request is well formed but breaks a business rule. Maps to 422.</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}