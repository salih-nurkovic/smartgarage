using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Entities.Reference;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IVehicleService : ICrudService<VehicleResponse, VehicleSearchRequest, VehicleRequest>
{
    Task<VehicleResponse> UpdateMileageAsync(int id, VehicleMileageRequest request, CancellationToken ct);
}

/// <summary>
/// Customer vehicles.
/// <para>
/// Ownership is enforced by the query itself: a customer's queries only ever contain their own
/// vehicles, so asking for someone else's by id finds nothing and answers 404. There is no
/// separate ownership check that a new method could forget to call.
/// </para>
/// </summary>
public sealed class VehicleService
    : CrudService<Vehicle, VehicleResponse, VehicleSearchRequest, VehicleRequest>, IVehicleService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public VehicleService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
        : base(db)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    protected override string NotFoundMessage => "Vozilo nije pronađeno.";

    protected override string DeletedMessage => "Vozilo je obrisano.";

    private bool IsStaff =>
        _currentUser.IsInRole(Roles.Administrator) || _currentUser.IsInRole(Roles.Employee);

    protected override IQueryable<Vehicle> Query
    {
        get
        {
            if (IsStaff)
            {
                return Db.Set<Vehicle>();
            }

            var userId = _currentUser.RequireUserId();
            return Db.Set<Vehicle>().Where(v => v.OwnerId == userId);
        }
    }

    public async Task<VehicleResponse> UpdateMileageAsync(
        int id,
        VehicleMileageRequest request,
        CancellationToken ct)
    {
        EnsureCanWrite();

        var vehicle = await LoadAsync(id, ct);

        EnsureMileageDoesNotGoBack(vehicle, request.CurrentMileageKm);
        SetMileage(vehicle, request.CurrentMileageKm);

        await Db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    protected override IQueryable<Vehicle> Filter(IQueryable<Vehicle> query, VehicleSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            query = query.Where(v =>
                v.RegistrationPlate.Contains(term)
                || v.Vin.Contains(term)
                || v.Brand.Name.Contains(term)
                || v.Model.Name.Contains(term)
                || v.Owner.FirstName.Contains(term)
                || v.Owner.LastName.Contains(term));
        }

        // For a customer the query is already limited to their own vehicles, so this filter
        // cannot widen what they see.
        if (search.OwnerId is { } ownerId)
        {
            query = query.Where(v => v.OwnerId == ownerId);
        }

        if (search.BrandId is { } brandId)
        {
            query = query.Where(v => v.BrandId == brandId);
        }

        return search.EngineTypeId is { } engineTypeId
            ? query.Where(v => v.EngineTypeId == engineTypeId)
            : query;
    }

    protected override IOrderedQueryable<Vehicle> Sort(IQueryable<Vehicle> query, VehicleSearchRequest search) =>
        search.OrderBy?.ToUpperInvariant() switch
        {
            "MILEAGE" => search.Descending
                ? query.OrderByDescending(v => v.CurrentMileageKm)
                : query.OrderBy(v => v.CurrentMileageKm),
            "YEAR" => search.Descending
                ? query.OrderByDescending(v => v.ProductionYear)
                : query.OrderBy(v => v.ProductionYear),
            "NAME" => SortByNameOrNewest(query, search, v => v.Brand.Name + " " + v.Model.Name),
            _ => query.OrderByDescending(v => v.Id),
        };

    protected override IQueryable<VehicleResponse> Project(IQueryable<Vehicle> query) =>
        query.Select(v => new VehicleResponse
        {
            Id = v.Id,
            OwnerFullName = v.Owner.FirstName + " " + v.Owner.LastName,
            BrandId = v.BrandId,
            BrandName = v.Brand.Name,
            ModelId = v.ModelId,
            ModelName = v.Model.Name,
            EngineTypeId = v.EngineTypeId,
            EngineTypeName = v.EngineType.Name,
            ProductionYear = v.ProductionYear,
            RegistrationPlate = v.RegistrationPlate,
            Vin = v.Vin,
            CurrentMileageKm = v.CurrentMileageKm,
            LastMileageUpdatedAtUtc = v.LastMileageUpdatedAtUtc,
            LastServiceAtUtc = v.Appointments
                .Where(a => a.Status == AppointmentStatus.Completed)
                .Max(a => a.CompletedAtUtc),
            NextServiceDueAtUtc = v.Reminders
                .Where(r => r.Status == ServiceReminderStatus.Pending && r.DueDateUtc != null)
                .Min(r => r.DueDateUtc),
            AppointmentCount = v.Appointments.Count,
        });

    protected override Vehicle Build(VehicleRequest request)
    {
        // The owner is the signed-in user and nothing else. No request field can change that.
        var vehicle = new Vehicle { OwnerId = _currentUser.RequireUserId() };
        Apply(vehicle, request);
        return vehicle;
    }

    protected override void Apply(Vehicle entity, VehicleRequest request)
    {
        entity.BrandId = request.BrandId;
        entity.ModelId = request.ModelId;
        entity.EngineTypeId = request.EngineTypeId;
        entity.ProductionYear = request.ProductionYear;
        entity.RegistrationPlate = Normalize(request.RegistrationPlate);
        entity.Vin = Normalize(request.Vin);

        if (entity.Id == 0 || entity.CurrentMileageKm != request.CurrentMileageKm)
        {
            SetMileage(entity, request.CurrentMileageKm);
        }
    }

    protected override async Task ValidateAsync(VehicleRequest request, Vehicle? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var id = existing?.Id;
        var plate = Normalize(request.RegistrationPlate);
        var vin = Normalize(request.Vin);

        var model = await Db.Set<VehicleModel>()
            .Where(m => m.Id == request.ModelId)
            .Select(m => new { m.BrandId })
            .SingleOrDefaultAsync(ct);

        if (!await Db.Set<VehicleBrand>().AnyAsync(b => b.Id == request.BrandId, ct))
        {
            errors["brandId"] = ["Izabrana marka vozila ne postoji."];
        }
        else if (model is null || model.BrandId != request.BrandId)
        {
            errors["modelId"] = ["Izabrani model ne pripada izabranoj marki."];
        }

        if (!await Db.Set<EngineType>().AnyAsync(e => e.Id == request.EngineTypeId, ct))
        {
            errors["engineTypeId"] = ["Izabrani tip motora ne postoji."];
        }

        // Checked against every owner's vehicles, not just the caller's: a VIN identifies one
        // car in the world. Archived vehicles are excluded, as in the unique index.
        var clashes = await Db.Set<Vehicle>()
            .IgnoreQueryFilters()
            .Where(v => !v.IsDeleted && v.Id != id && (v.Vin == vin || v.RegistrationPlate == plate))
            .Select(v => new { v.Vin, v.RegistrationPlate })
            .ToListAsync(ct);

        if (clashes.Any(c => c.Vin == vin))
        {
            errors["vin"] = ["Vozilo sa ovim brojem šasije (VIN) je već registrovano."];
        }

        if (clashes.Any(c => c.RegistrationPlate == plate))
        {
            errors["registrationPlate"] = ["Vozilo sa ovom registarskom oznakom je već registrovano."];
        }

        if (existing is not null && request.CurrentMileageKm < existing.CurrentMileageKm)
        {
            errors["currentMileageKm"] = [MileageMessage(existing.CurrentMileageKm)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    // A vehicle with appointments carries payments and an audit trail that must stay readable.
    protected override async Task<string?> GetDeleteBlockerAsync(Vehicle entity, CancellationToken ct)
    {
        var appointments = await Db.Set<Appointment>()
            .IgnoreQueryFilters()
            .CountAsync(a => a.VehicleId == entity.Id, ct);

        return appointments == 0
            ? null
            : $"Vozilo se ne može obrisati jer ima {appointments} rezervacija u historiji servisa.";
    }

    // Employees look vehicles up while working on appointments, but the record belongs to the
    // customer: only its owner or an administrator may change it.
    protected override void EnsureCanWrite()
    {
        if (_currentUser.IsInRole(Roles.Employee))
        {
            throw new ForbiddenException("Zaposlenici mogu pregledati vozila, ali ih ne mogu mijenjati.");
        }
    }

    private void SetMileage(Vehicle vehicle, int mileageKm)
    {
        vehicle.CurrentMileageKm = mileageKm;
        vehicle.LastMileageUpdatedAtUtc = _clock.UtcNow;
    }

    private static void EnsureMileageDoesNotGoBack(Vehicle vehicle, int mileageKm)
    {
        if (mileageKm < vehicle.CurrentMileageKm)
        {
            throw new ValidationException("currentMileageKm", MileageMessage(vehicle.CurrentMileageKm));
        }
    }

    private static string MileageMessage(int currentKm) =>
        $"Kilometraža ne može biti manja od trenutno upisane ({currentKm} km).";

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
