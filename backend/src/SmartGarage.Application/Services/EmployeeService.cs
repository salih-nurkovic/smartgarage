using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeResponse>> GetAsync(EmployeeSearchRequest search, CancellationToken ct);

    Task<EmployeeResponse> GetByIdAsync(int id, CancellationToken ct);

    Task<EmployeeResponse> CreateAsync(EmployeeCreateRequest request, CancellationToken ct);

    Task<EmployeeResponse> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken ct);

    Task<EmployeeResponse> SetScheduleAsync(int id, EmployeeScheduleRequest request, CancellationToken ct);

    Task<MessageResponse> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// Employees: an Identity account carrying the Employee role, plus the employment record and
/// weekly schedule that belong to it.
/// <para>
/// This is the only place the Employee role is granted. Creating the account and the record
/// in one transaction is what guarantees there is never an employee who cannot be scheduled,
/// or a schedule that belongs to nobody.
/// </para>
/// </summary>
public sealed class EmployeeService : IEmployeeService
{
    private static readonly DayOfWeek[] Weekend = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppDbContext _db;
    private readonly ISessionService _sessions;
    private readonly BusinessHoursOptions _hours;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        UserManager<ApplicationUser> userManager,
        IAppDbContext db,
        ISessionService sessions,
        IOptions<BusinessHoursOptions> hours,
        ILogger<EmployeeService> logger)
    {
        _userManager = userManager;
        _db = db;
        _sessions = sessions;
        _hours = hours.Value;
        _logger = logger;
    }

    public async Task<PagedResult<EmployeeResponse>> GetAsync(EmployeeSearchRequest search, CancellationToken ct)
    {
        var query = _db.Set<EmployeeProfile>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            query = query.Where(e =>
                e.User.FirstName.Contains(term)
                || e.User.LastName.Contains(term)
                || e.User.UserName!.Contains(term)
                || e.Position.Contains(term)
                || e.Specialization!.Contains(term));
        }

        if (search.IsActive is { } isActive)
        {
            query = query.Where(e => e.User.IsActive == isActive);
        }

        if (search.IsAvailableForAssignment is { } isAvailable)
        {
            query = query.Where(e => e.IsAvailableForAssignment == isAvailable);
        }

        var totalCount = await query.CountAsync(ct);

        var ordered = string.Equals(search.OrderBy, "newest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(e => e.Id)
            : search.Descending
                ? query.OrderByDescending(e => e.User.LastName).ThenByDescending(e => e.User.FirstName)
                : query.OrderBy(e => e.User.LastName).ThenBy(e => e.User.FirstName);

        var items = await Project(ordered.Skip(search.Skip).Take(search.ClampedPageSize)).ToListAsync(ct);

        return new PagedResult<EmployeeResponse>(
            items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<EmployeeResponse> GetByIdAsync(int id, CancellationToken ct) =>
        await Project(_db.Set<EmployeeProfile>().AsNoTracking().Where(e => e.Id == id))
            .SingleOrDefaultAsync(ct)
        ?? throw EmployeeNotFound();

    public async Task<EmployeeResponse> CreateAsync(EmployeeCreateRequest request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();
        var email = request.Email.Trim();

        await _db.EnsureUniqueAsync(
            _userManager.NormalizeName(userName),
            _userManager.NormalizeEmail(email),
            exceptUserId: null,
            ct);

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IsActive = true,
        };

        var profile = new EmployeeProfile
        {
            User = user,
            Position = request.Position.Trim(),
            Specialization = NullIfBlank(request.Specialization),
            HireDateUtc = ToUtc(request.HireDate),
            IsAvailableForAssignment = true,
            Schedules = DefaultWeek(),
        };

        await _db.ExecuteInTransactionAsync(
            async token =>
            {
                (await _userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
                (await _userManager.AddToRoleAsync(user, Roles.Employee)).ThrowIfFailed();

                _db.Set<EmployeeProfile>().Add(profile);
                await _db.SaveChangesAsync(token);
            },
            ct);

        _logger.LogInformation("Employee {UserName} created with profile {ProfileId}.", userName, profile.Id);

        return await GetByIdAsync(profile.Id, ct);
    }

    public async Task<EmployeeResponse> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken ct)
    {
        var profile = await _db.Set<EmployeeProfile>()
                .Include(e => e.User)
                .SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw EmployeeNotFound();

        var email = request.Email.Trim();

        await _db.EnsureUniqueAsync(
            normalizedUserName: null,
            _userManager.NormalizeEmail(email),
            exceptUserId: profile.UserId,
            ct);

        profile.User.Email = email;
        profile.User.FirstName = request.FirstName.Trim();
        profile.User.LastName = request.LastName.Trim();
        profile.User.PhoneNumber = request.PhoneNumber.Trim();

        profile.Position = request.Position.Trim();
        profile.Specialization = NullIfBlank(request.Specialization);
        profile.HireDateUtc = ToUtc(request.HireDate);
        profile.IsAvailableForAssignment = request.IsAvailableForAssignment;

        // UpdateAsync refreshes the normalised e-mail and saves the whole unit of work, the
        // profile changes included, in one statement batch.
        (await _userManager.UpdateAsync(profile.User)).ThrowIfFailed();

        return await GetByIdAsync(id, ct);
    }

    public async Task<EmployeeResponse> SetScheduleAsync(
        int id,
        EmployeeScheduleRequest request,
        CancellationToken ct)
    {
        var profile = await _db.Set<EmployeeProfile>()
                .Include(e => e.Schedules)
                .SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw EmployeeNotFound();

        // Rows are updated in place. There is exactly one per weekday, and the validator has
        // already made sure the request names all seven.
        foreach (var day in request.Days)
        {
            var row = profile.Schedules.SingleOrDefault(s => s.DayOfWeek == day.DayOfWeek);

            if (row is null)
            {
                row = new EmployeeSchedule { DayOfWeek = day.DayOfWeek };
                profile.Schedules.Add(row);
            }

            row.StartTime = day.StartTime;
            row.EndTime = day.EndTime;
            row.IsWorkingDay = day.IsWorkingDay;
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<MessageResponse> DeleteAsync(int id, CancellationToken ct)
    {
        // Loaded on its own, without the profile: the account is the only row this changes.
        var user = await _db.Set<EmployeeProfile>()
                .Where(e => e.Id == id)
                .Select(e => e.User)
                .SingleOrDefaultAsync(ct)
            ?? throw EmployeeNotFound();

        var appointments = await _db.Set<Appointment>()
            .IgnoreQueryFilters()
            .CountAsync(a => a.AssignedEmployeeId == id, ct);

        // Their name is on appointments customers and reports still show. Deactivating the
        // account keeps the history intact and still locks the person out.
        if (appointments > 0)
        {
            throw new ConflictException(
                $"Zaposlenik se ne može obrisati jer je dodijeljen na {appointments} rezervacija. "
                + "Deaktivirajte njegov nalog da se više ne može prijaviti ni dobiti nove poslove.");
        }

        await _db.ExecuteInTransactionAsync(
            async token =>
            {
                // The account is archived rather than erased; the soft-delete interceptor
                // turns this removal into a flag, and the profile disappears with it.
                _db.Users.Remove(user);
                await _db.SaveChangesAsync(token);

                await _sessions.RevokeAllForUserAsync(
                    user.Id,
                    SecurityRules.RevocationReasons.AccountDeactivated,
                    exceptJwtId: null,
                    token);
            },
            ct);

        return new MessageResponse("Zaposlenik je obrisan.");
    }

    // Job count and rating are subqueries of the same statement, so a page of employees is one
    // round trip however many appointments each of them has.
    private IQueryable<EmployeeResponse> Project(IQueryable<EmployeeProfile> query)
    {
        var reviews = _db.Set<Review>();

        return query.Select(e => new EmployeeResponse
        {
            Id = e.Id,
            UserId = e.UserId,
            UserName = e.User.UserName!,
            FirstName = e.User.FirstName,
            LastName = e.User.LastName,
            Email = e.User.Email!,
            PhoneNumber = e.User.PhoneNumber!,
            Position = e.Position,
            Specialization = e.Specialization,
            HireDateUtc = e.HireDateUtc,
            IsActive = e.User.IsActive,
            IsAvailableForAssignment = e.IsAvailableForAssignment,
            CompletedJobs = e.AssignedAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            AverageRating = reviews
                .Where(r => r.AssignedEmployeeId == e.Id && r.IsVisible)
                .Average(r => (double?)r.Rating),
            Schedule = e.Schedules
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new EmployeeScheduleDay
                {
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsWorkingDay = s.IsWorkingDay,
                })
                .ToList(),
        });
    }

    // Every day gets a row, weekends as non-working, so the availability calculation never has
    // to guess what a missing day means.
    private List<EmployeeSchedule> DefaultWeek() =>
        Enum.GetValues<DayOfWeek>()
            .Select(day => new EmployeeSchedule
            {
                DayOfWeek = day,
                StartTime = _hours.WorkdayStart,
                EndTime = _hours.WorkdayEnd,
                IsWorkingDay = !Weekend.Contains(day),
            })
            .ToList();

    private static DateTime ToUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotFoundException EmployeeNotFound() => new("Zaposlenik nije pronađen.");
}
