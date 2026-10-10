using SmartGarage.Application.Abstractions;

namespace SmartGarage.API.Extensions;

public static class FormFileExtensions
{
    /// <summary>
    /// Hands an upload to the service layer without the HTTP type. The caller opens and
    /// disposes the stream, so its lifetime is visible in the action that owns it.
    /// </summary>
    public static FileUpload ToUpload(this IFormFile file, Stream content) =>
        new(content, file.ContentType, file.Length);
}
