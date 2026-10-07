namespace SmartGarage.Domain.Common;

/// <summary>Base for every entity with a surrogate integer key.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
}
