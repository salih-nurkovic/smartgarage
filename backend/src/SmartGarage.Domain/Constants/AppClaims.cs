namespace SmartGarage.Domain.Constants;

/// <summary>
/// Claim names written into the access token and read back from it. Declared once so the code
/// that issues a token and the code that reads it can never disagree on a name.
/// </summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string JwtId = "jti";
    public const string UserName = "unique_name";
    public const string Email = "email";
    public const string FirstName = "given_name";
    public const string LastName = "family_name";
    public const string Role = "role";
}
