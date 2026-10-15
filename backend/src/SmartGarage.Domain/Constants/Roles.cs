namespace SmartGarage.Domain.Constants;

/// <summary>
/// Role names, declared once. The course instructions forbid magic role strings scattered
/// through attributes ("Magic stringove za role treba smjestiti u staticku klasu"), and the
/// names here must match the names written by the seeder exactly.
/// </summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Employee = "Employee";
    public const string Customer = "Customer";

    /// <summary>
    /// Not a role a person can hold: the name the Worker acts under when it changes something
    /// on a schedule. It is deliberately absent from <see cref="All"/>, so it is never seeded and
    /// can never be assigned to an account.
    /// </summary>
    public const string System = "System";

    public static readonly IReadOnlyList<string> All = [Administrator, Employee, Customer];
}
