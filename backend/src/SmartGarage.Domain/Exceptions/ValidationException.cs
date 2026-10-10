namespace SmartGarage.Domain.Exceptions;

/// <summary>
/// Input failed validation. Carries messages keyed by field name so both clients can show each
/// one under the control it belongs to, which the instructions require. Maps to 400.
/// </summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Uneseni podaci nisu ispravni. Provjerite označena polja.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}