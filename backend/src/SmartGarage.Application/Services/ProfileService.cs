using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

/// <summary>
/// The signed-in user's own profile. Nothing here takes a user id: it always comes from the
/// token, so one user can never read or edit another's profile through these methods.
/// </summary>
public interface IProfileService
{
    Task<UserResponse> GetAsync(CancellationToken ct);

    Task<UserResponse> UpdateAsync(ProfileUpdateRequest request, CancellationToken ct);

    Task<FileDownload> GetImageAsync(CancellationToken ct);

    Task<UserResponse> SetImageAsync(FileUpload file, CancellationToken ct);
}

public sealed class ProfileService : IProfileService
{
    private const string ImageFolder = "avatars";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IImageStore _images;

    public ProfileService(
        UserManager<ApplicationUser> userManager,
        IAppDbContext db,
        ICurrentUserService currentUser,
        IImageStore images)
    {
        _userManager = userManager;
        _db = db;
        _currentUser = currentUser;
        _images = images;
    }

    public async Task<UserResponse> GetAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        return await _db.Users
                   .AsNoTracking()
                   .Where(u => u.Id == userId)
                   .SelectResponse(_db)
                   .SingleOrDefaultAsync(ct)
               ?? throw ProfileNotFound();
    }

    public async Task<UserResponse> UpdateAsync(ProfileUpdateRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ProfileNotFound();

        var email = request.Email.Trim();

        await _db.EnsureUniqueAsync(
            normalizedUserName: null, _userManager.NormalizeEmail(email), exceptUserId: userId, ct);

        user.Email = email;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();

        (await _userManager.UpdateAsync(user)).ThrowIfFailed();

        return UserResponse.From(user, await _userManager.GetRolesAsync(user));
    }

    // The path is looked up by the id in the token, so this can only ever return the caller's
    // own picture. That is the ownership check the instructions ask for on downloads.
    public async Task<FileDownload> GetImageAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var path = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ProfileImagePath)
            .SingleOrDefaultAsync(ct);

        return _images.Open(path) ?? throw new NotFoundException("Profilna slika nije postavljena.");
    }

    public async Task<UserResponse> SetImageAsync(FileUpload file, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ProfileNotFound();

        var previous = user.ProfileImagePath;

        user.ProfileImagePath = await _images.SaveAsync(ImageFolder, file, ct);
        await _db.SaveChangesAsync(ct);

        _images.Delete(previous);

        return UserResponse.From(user, await _userManager.GetRolesAsync(user));
    }

    private static NotFoundException ProfileNotFound() => new("Korisnički profil nije pronađen.");
}
