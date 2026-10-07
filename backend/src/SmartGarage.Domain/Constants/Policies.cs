namespace SmartGarage.Domain.Constants;

/// <summary>
/// Authorization policy names. Controllers reference these rather than role strings, so the
/// mapping from policy to roles lives in exactly one place (the DI registration).
/// </summary>
public static class Policies
{
    /// <summary>Administrators only: user management, reference CRUD, refunds, reports.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Administrators and employees: the desktop application's working surface.</summary>
    public const string StaffOnly = nameof(StaffOnly);

    /// <summary>Customers only: the mobile application's surface.</summary>
    public const string CustomerOnly = nameof(CustomerOnly);
}
