using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Infrastructure.Storage;

/// <summary>
/// Keeps uploaded images on the local disk (a Docker volume in the container).
/// <para>
/// The instructions require an upload to be checked by MIME type and magic bytes, not by
/// extension. The extension a client sends is ignored altogether here: the stored name is a
/// new GUID and the extension is derived from what the bytes actually are.
/// </para>
/// </summary>
public sealed class LocalImageStore : IImageStore
{
    private const string UploadsSegment = "uploads";
    private const int HeaderLength = 12;
    private const int BytesPerMegabyte = 1024 * 1024;

    private static readonly ImageFormat[] Formats =
    [
        new("image/jpeg", ".jpg", header => header is [0xFF, 0xD8, 0xFF, ..]),
        new("image/png", ".png", header => header is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..]),
        new("image/webp", ".webp", header =>
            header is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50]),
    ];

    private readonly string _root;
    private readonly int _maxMegabytes;

    public LocalImageStore(IConfiguration configuration, IHostEnvironment environment)
    {
        // Read once, here. A relative UPLOADS_ROOT is taken from the application's own folder.
        _root = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, configuration["UPLOADS_ROOT"] ?? "wwwroot"));
        _maxMegabytes = configuration.GetValue("MAX_UPLOAD_SIZE_MB", 5);
    }

    public async Task<string> SaveAsync(string folder, FileUpload file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            throw Refused("Datoteka je prazna. Izaberite sliku.");
        }

        if (file.Length > (long)_maxMegabytes * BytesPerMegabyte)
        {
            throw Refused($"Slika je prevelika. Najveća dozvoljena veličina je {_maxMegabytes} MB.");
        }

        var header = new byte[HeaderLength];
        var read = await file.Content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, ct);

        var format = Formats.FirstOrDefault(f => f.Matches(header.AsSpan(0, read)))
            ?? throw Refused("Datoteka nije slika. Dozvoljeni formati su JPEG, PNG i WebP.");

        if (!string.Equals(file.ContentType, format.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw Refused("Sadržaj datoteke ne odgovara navedenom tipu. Izaberite ispravnu sliku.");
        }

        var storedPath = $"/{UploadsSegment}/{folder}/{Guid.NewGuid():N}{format.Extension}";
        var fullPath = Resolve(storedPath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var target = new FileStream(
                         fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await target.WriteAsync(header.AsMemory(0, read), ct);
            await file.Content.CopyToAsync(target, ct);
        }

        return storedPath;
    }

    public FileDownload? Open(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath))
        {
            return null;
        }

        var fullPath = Resolve(storedPath);
        var format = Formats.FirstOrDefault(
            f => fullPath.EndsWith(f.Extension, StringComparison.OrdinalIgnoreCase));

        return format is null || !File.Exists(fullPath)
            ? null
            : new FileDownload(
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true),
                format.ContentType);
    }

    public void Delete(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath))
        {
            return;
        }

        var fullPath = Resolve(storedPath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    // Stored paths come from the database, but they are still checked: nothing outside the
    // uploads folder is ever opened or deleted through this class.
    private string Resolve(string storedPath)
    {
        var uploadsRoot = Path.Combine(_root, UploadsSegment) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_root, storedPath.TrimStart('/', '\\')));

        return fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : throw new InvalidOperationException("A stored image path points outside the uploads folder.");
    }

    private static ValidationException Refused(string message) => new("file", message);

    private sealed record ImageFormat(string ContentType, string Extension, HeaderPredicate Matches);

    private delegate bool HeaderPredicate(ReadOnlySpan<byte> header);
}
