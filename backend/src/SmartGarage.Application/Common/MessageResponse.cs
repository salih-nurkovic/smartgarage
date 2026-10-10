namespace SmartGarage.Application.Common;

/// <summary>
/// A confirmation for operations that have nothing else to return. The text is specific to the
/// operation and is shown to the user as is.
/// </summary>
public sealed record MessageResponse(string Message);
