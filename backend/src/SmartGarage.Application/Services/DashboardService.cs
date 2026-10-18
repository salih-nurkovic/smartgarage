using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Dashboard;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CancellationToken ct);
}

/// <summary>
/// The figures on the desktop dashboard. Each is one aggregate query in SQL; nothing is
/// counted by loading rows into memory.
/// <para>
/// Revenue is defined exactly as on the payments screen: what Stripe charged for successful
/// payments, less refunds that went through. A payment counts in the month it was paid and a
/// refund in the month it completed.
/// </para>
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private const int RevenueMonths = 6;
    private const int DistributionMonths = 6;
    private const int DistributionTopServices = 4;
    private const string OtherServices = "Ostalo";

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public DashboardService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // "This month so far" against the same stretch of last month, so a comparison on the
        // 5th is not five days against thirty.
        var previousStart = monthStart.AddMonths(-1);
        var previousEnd = previousStart + (now - monthStart);
        if (previousEnd > monthStart)
        {
            previousEnd = monthStart;
        }

        KpiResponse? monthRevenue = null;
        IReadOnlyList<MonthlyRevenueResponse>? revenueByMonth = null;

        if (_currentUser.IsInRole(Roles.Administrator))
        {
            var current = await RevenueAsync(monthStart, now, ct);
            var previous = await RevenueAsync(previousStart, previousEnd, ct);
            monthRevenue = new KpiResponse(current, Change(current, previous));
            revenueByMonth = await RevenueByMonthAsync(monthStart, now, ct);
        }

        return new DashboardResponse
        {
            Customers = await CustomersAsync(monthStart, ct),
            ActiveAppointments = await ActiveAppointmentsAsync(monthStart, now, previousStart, previousEnd, ct),
            CompletedServices = await CompletedServicesAsync(monthStart, now, previousStart, previousEnd, ct),
            MonthRevenue = monthRevenue,
            RevenueByMonth = revenueByMonth,
            ServiceDistribution = await ServiceDistributionAsync(monthStart.AddMonths(-(DistributionMonths - 1)), ct),
        };
    }

    // Total customers, and how much that total grew since the month began.
    private async Task<KpiResponse> CustomersAsync(DateTime monthStart, CancellationToken ct)
    {
        var customerRoleId = await _db.Roles
            .Where(r => r.Name == Roles.Customer)
            .Select(r => r.Id)
            .SingleAsync(ct);

        var customers = _db.Users.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == customerRoleId));

        var totals = await customers
            .GroupBy(_ => 1)
            .Select(g => new { All = g.Count(), BeforeMonth = g.Count(u => u.CreatedAtUtc < monthStart) })
            .SingleOrDefaultAsync(ct);

        return totals is null
            ? new KpiResponse(0, null)
            : new KpiResponse(totals.All, Change(totals.All, totals.BeforeMonth));
    }

    // Open appointments right now; the change is in bookings made this month against last.
    private async Task<KpiResponse> ActiveAppointmentsAsync(
        DateTime monthStart,
        DateTime now,
        DateTime previousStart,
        DateTime previousEnd,
        CancellationToken ct)
    {
        var counts = await _db.Set<Appointment>()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Active = g.Count(a => a.Status == AppointmentStatus.Pending
                                      || a.Status == AppointmentStatus.Confirmed
                                      || a.Status == AppointmentStatus.InProgress),
                BookedNow = g.Count(a => a.CreatedAtUtc >= monthStart && a.CreatedAtUtc < now),
                BookedBefore = g.Count(a => a.CreatedAtUtc >= previousStart && a.CreatedAtUtc < previousEnd),
            })
            .SingleOrDefaultAsync(ct);

        return counts is null
            ? new KpiResponse(0, null)
            : new KpiResponse(counts.Active, Change(counts.BookedNow, counts.BookedBefore));
    }

    private async Task<KpiResponse> CompletedServicesAsync(
        DateTime monthStart,
        DateTime now,
        DateTime previousStart,
        DateTime previousEnd,
        CancellationToken ct)
    {
        var counts = await _db.Set<Appointment>()
            .Where(a => a.Status == AppointmentStatus.Completed)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                All = g.Count(),
                Now = g.Count(a => a.CompletedAtUtc >= monthStart && a.CompletedAtUtc < now),
                Before = g.Count(a => a.CompletedAtUtc >= previousStart && a.CompletedAtUtc < previousEnd),
            })
            .SingleOrDefaultAsync(ct);

        return counts is null
            ? new KpiResponse(0, null)
            : new KpiResponse(counts.All, Change(counts.Now, counts.Before));
    }

    private async Task<decimal> RevenueAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var charged = await _db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAtUtc >= from && p.PaidAtUtc < to)
            .SumAsync(p => (decimal?)p.AmountChargedStripe, ct) ?? 0;

        var refunded = await _db.Set<Refund>()
            .Where(r => r.Status == RefundStatus.Succeeded && r.CompletedAtUtc >= from && r.CompletedAtUtc < to)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;

        return charged - refunded;
    }

    // The last six months, the current one included, one row per month even when it is empty.
    private async Task<IReadOnlyList<MonthlyRevenueResponse>> RevenueByMonthAsync(
        DateTime monthStart,
        DateTime now,
        CancellationToken ct)
    {
        var from = monthStart.AddMonths(-(RevenueMonths - 1));

        var charged = await _db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAtUtc >= from && p.PaidAtUtc < now)
            .GroupBy(p => new { p.PaidAtUtc!.Value.Year, p.PaidAtUtc.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.AmountChargedStripe) })
            .ToListAsync(ct);

        var refunded = await _db.Set<Refund>()
            .Where(r => r.Status == RefundStatus.Succeeded && r.CompletedAtUtc >= from && r.CompletedAtUtc < now)
            .GroupBy(r => new { r.CompletedAtUtc!.Value.Year, r.CompletedAtUtc.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(r => r.Amount) })
            .ToListAsync(ct);

        return Enumerable.Range(0, RevenueMonths)
            .Select(from.AddMonths)
            .Select(month => new MonthlyRevenueResponse(
                month.Year,
                month.Month,
                charged.Where(c => c.Year == month.Year && c.Month == month.Month).Sum(c => c.Amount)
                - refunded.Where(r => r.Year == month.Year && r.Month == month.Month).Sum(r => r.Amount)))
            .ToList();
    }

    // Work done over the last months by service category - the most frequent categories by
    // name, the rest together as "Ostalo". By category rather than by single service: with dozens
    // of services the top few cover too little of the whole to say anything.
    private async Task<IReadOnlyList<ServiceShareResponse>> ServiceDistributionAsync(DateTime from, CancellationToken ct)
    {
        var counts = await _db.Set<AppointmentItem>()
            .Where(i => i.Appointment.Status == AppointmentStatus.Completed && i.Appointment.CompletedAtUtc >= from)
            .GroupBy(i => i.Service.Category.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var total = counts.Sum(c => c.Count);
        if (total == 0)
        {
            return [];
        }

        var shares = counts
            .Take(DistributionTopServices)
            .Select(c => (c.Name, c.Count))
            .ToList();

        var rest = total - shares.Sum(s => s.Count);
        if (rest > 0)
        {
            shares.Add((OtherServices, rest));
        }

        return shares
            .Select(s => new ServiceShareResponse(s.Name, s.Count, Math.Round(100m * s.Count / total, 1)))
            .ToList();
    }

    private static decimal? Change(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round(100m * (current - previous) / previous, 1);
}
