namespace SmartGarage.Application.Abstractions;

/// <summary>An uploaded file as the service layer sees it, free of any HTTP type.</summary>
public sealed record FileUpload(Stream Content, string ContentType, long Length);

/// <summary>A stored file being handed back. Whoever receives it disposes the stream.</summary>
public sealed record FileDownload(Stream Content, string ContentType);

/// <summary>
/// Stores images outside the reach of the static file middleware, so a stored image can only
/// be read through an endpoint that checks who is asking.
/// </summary>
public interface IImageStore
{
    /// <summary>
    /// Validates and stores an image and returns the path to keep in the database.
    /// The file's real type is established from its leading bytes; the name and the declared
    /// content type are not trusted. Throws a validation error keyed "file" when it is refused.
    /// </summary>
    Task<string> SaveAsync(string folder, FileUpload file, CancellationToken ct);

    /// <summary>Opens a stored image, or returns null when there is none at that path.</summary>
    FileDownload? Open(string? storedPath);

    void Delete(string? storedPath);
}
