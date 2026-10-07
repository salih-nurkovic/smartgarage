using Microsoft.AspNetCore.Identity;
using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Every human in the system: customer, employee or administrator. Extends ASP.NET Identity so
/// there is exactly one person table rather than parallel ones.
/// <para>
/// Login is by <see cref="IdentityUser{TKey}.UserName"/>, not e-mail, because the course
/// instructions prescribe the README credential table in terms of "Korisnicko ime".
/// </para>
/// </summary>
public class ApplicationUser : IdentityUser<int>, IAuditable, ISoftDeletable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImagePath { get; set; }

    /// <summary>
    /// Administrators activate and deactivate accounts with this. Checked at login and again
    /// when a token is validated, so deactivating someone takes effect immediately rather than
    /// when their current token happens to expire.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public EmployeeProfile? EmployeeProfile { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<UserServiceSignal> ServiceSignals { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Convenience for lists and PDF reports, never persisted.</summary>
    public string FullName => $"{FirstName} {LastName}";
}
