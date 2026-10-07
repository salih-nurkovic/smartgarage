using Microsoft.AspNetCore.Identity;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Identity role with an integer key, so it matches <see cref="ApplicationUser"/>. Role names
/// come from the Roles constants class and are never typed inline.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }
}
