namespace SmartGarage.Domain.Common;

/// <summary>
/// Audit trail required by the course instructions, section 7: every record must be able to
/// say who created or changed it and when. Filled automatically by AuditingInterceptor, so no
/// service has to remember to set these.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    int? CreatedByUserId { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
    int? ModifiedByUserId { get; set; }
}
