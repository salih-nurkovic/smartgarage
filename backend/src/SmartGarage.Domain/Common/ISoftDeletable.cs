namespace SmartGarage.Domain.Common;

/// <summary>
/// Marks an entity that is archived rather than physically removed when other records depend
/// on it. The instructions require deletion to respect foreign key order and to explain itself
/// to the user instead of failing.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    int? DeletedByUserId { get; set; }
}
