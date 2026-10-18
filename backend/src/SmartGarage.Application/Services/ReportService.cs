using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Reports;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IReportService
{
    Task<RevenueReportResponse> GetRevenueAsync(ReportRangeRequest request, CancellationToken ct);

    /// <summary>
    /// The appointment figures; <paramref name="includeRows"/> also lists the appointments
    /// themselves, which only the PDF needs.
    /// </summary>
    Task<AppointmentReportResponse> GetAppointmentsAsync(AppointmentReportRequest request, bool includeRows, CancellationToken ct);
}

/// <summary>
/// The two administration reports. Every figure is an aggregate computed by SQL Server - a
/// <c>GroupBy</c> or a <c>Sum</c> - so the cost does not grow with the number of rows read into
/// memory.
/// <para>
/// Revenue follows the rule used everywhere else: what Stripe charged for successful payments,
/// counted on the day it was paid, less refunds that went through, counted on the day they
/// completed. Every figure is compared with the period of the same length just before.
/// </para>
/// </summary>
public sealed class ReportService : IReportService
{
    private const int MaxRangeDays = 731;
    private const int DailyUpToDays = 62;
    private const int TopServices = 15;
    private const int MaxRefunds = 100;
    private const int MaxReasons = 10;
    private const int MaxRows = 500;

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly string _currency;

    public ReportService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IOptions<GarageOptions> garage)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _currency = garage.Value.DisplayCurrency;
    }

    public async Task<RevenueReportResponse> GetRevenueAsync(ReportRangeRequest request, CancellationToken ct)
    {
        var range = Resolve(request);

        var current = await RevenueTotalsAsync(range.Start, range.End, ct);
        var previous = await RevenueTotalsAsync(range.PreviousStart, range.Start, ct);

        var paidItems = _db.Set<AppointmentItem>().Where(i =>
            i.Appointment.Payments.Any(p => p.Status == PaymentStatus.Succeeded
                                            && p.PaidAtUtc >= range.Start
                                            && p.PaidAtUtc < range.End));

        var byCategory = await paidItems
            .GroupBy(i => i.Service.Category.Name)
            .Select(g => new { Name = g.Key, Count = g.Count(), Amount = g.Sum(i => i.LineTotal) })
            .OrderByDescending(g => g.Amount)
            .ToListAsync(ct);

        var topServices = await paidItems
            .GroupBy(i => i.Service.Name)
            .Select(g => new { Name = g.Key, Count = g.Count(), Amount = g.Sum(i => i.LineTotal) })
            .OrderByDescending(g => g.Amount)
            .Take(TopServices)
            .ToListAsync(ct);

        var categoryTotal = byCategory.Sum(c => c.Amount);

        var refunds = await _db.Set<Refund>()
            .Where(r => r.Status == RefundStatus.Succeeded && r.CompletedAtUtc >= range.Start && r.CompletedAtUtc < range.End)
            .OrderByDescending(r => r.CompletedAtUtc)
            .Take(MaxRefunds)
            .Select(r => new RefundRow(
                r.Payment.PaymentNumber,
                r.CompletedAtUtc!.Value,
                r.Amount,
                r.Reason,
                r.RequestedByUser.FirstName + " " + r.RequestedByUser.LastName))
            .ToListAsync(ct);

        return new RevenueReportResponse
        {
            From = range.From,
            To = range.To,
            GeneratedAtUtc = _clock.UtcNow,
            GeneratedBy = await GeneratedByAsync(ct),
            Currency = _currency,
            GrossRevenue = new ReportFigure(current.Gross, Change(current.Gross, previous.Gross)),
            RefundedAmount = current.Refunded,
            NetRevenue = new ReportFigure(current.Net, Change(current.Net, previous.Net)),
            PaymentCount = current.Count,
            AverageTransaction = new ReportFigure(current.Average, Change(current.Average, previous.Average)),
            ByPeriod = await ByPeriodAsync(range, _db.Set<Appointment>(), ct),
            ByCategory = byCategory
                .Select(c => new ReportShareRow(c.Name, c.Count, c.Amount, Percent(c.Amount, categoryTotal)))
                .ToList(),
            TopServices = topServices
                .Select(s => new ReportShareRow(s.Name, s.Count, s.Amount, Percent(s.Amount, categoryTotal)))
                .ToList(),
            Refunds = refunds,
        };
    }

    public async Task<AppointmentReportResponse> GetAppointmentsAsync(
        AppointmentReportRequest request,
        bool includeRows,
        CancellationToken ct)
    {
        var range = Resolve(request);

        IQueryable<Appointment> Filtered(DateTime from, DateTime to)
        {
            var query = _db.Set<Appointment>().Where(a => a.ScheduledStartUtc >= from && a.ScheduledStartUtc < to);
            if (request.Status is { } status)
            {
                query = query.Where(a => a.Status == status);
            }
            if (request.EmployeeId is { } employeeId)
            {
                query = query.Where(a => a.AssignedEmployeeId == employeeId);
            }
            return query;
        }

        var appointments = Filtered(range.Start, range.End);
        var before = Filtered(range.PreviousStart, range.Start);

        var byStatus = await appointments
            .GroupBy(a => a.Status)
            .Select(g => new StatusCountRow(g.Key, g.Count()))
            .ToListAsync(ct);
        var total = byStatus.Sum(s => s.Count);
        var totalBefore = await before.CountAsync(ct);
        var completed = byStatus.Where(s => s.Status == AppointmentStatus.Completed).Sum(s => s.Count);
        var cancelled = byStatus.Where(s => s.Status == AppointmentStatus.Cancelled).Sum(s => s.Count);

        var averagePrice = await appointments.Where(a => a.Status != AppointmentStatus.Cancelled)
            .AverageAsync(a => (decimal?)a.TotalPrice, ct) ?? 0;
        var averagePriceBefore = await before.Where(a => a.Status != AppointmentStatus.Cancelled)
            .AverageAsync(a => (decimal?)a.TotalPrice, ct) ?? 0;

        var averageDuration = await appointments
            .Select(a => (double?)a.Items.Sum(i => i.DurationMinutesSnapshot * i.Quantity))
            .AverageAsync(ct) ?? 0;

        var rating = await RatingAsync(range.Start, range.End, ct);
        var ratingBefore = await RatingAsync(range.PreviousStart, range.Start, ct);

        var byCategory = await appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled)
            .SelectMany(a => a.Items)
            .GroupBy(i => i.Service.Category.Name)
            .Select(g => new { Name = g.Key, Count = g.Count(), Amount = g.Sum(i => i.LineTotal) })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);
        var itemCount = byCategory.Sum(c => c.Count);

        var byEmployee = await appointments
            .Where(a => a.AssignedEmployeeId != null)
            .GroupBy(a => a.AssignedEmployee!.User.FirstName + " " + a.AssignedEmployee.User.LastName)
            .Select(g => new
            {
                Name = g.Key,
                Total = g.Count(),
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed),
                Closed = g.Count(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled),
            })
            .OrderByDescending(e => e.Total)
            .ToListAsync(ct);

        var reasons = await appointments
            .Where(a => a.Status == AppointmentStatus.Cancelled && a.CancellationReason != null)
            .GroupBy(a => a.CancellationReason!)
            .OrderByDescending(g => g.Count())
            .Take(MaxReasons)
            .Select(g => new ReasonRow(g.Key, g.Count()))
            .ToListAsync(ct);

        var rows = includeRows
            ? await appointments
                .OrderByDescending(a => a.ScheduledStartUtc)
                .Take(MaxRows)
                .Select(a => new AppointmentRow(
                    a.AppointmentNumber,
                    a.ScheduledStartUtc,
                    a.Customer.FirstName + " " + a.Customer.LastName,
                    a.Vehicle.Brand.Name + " " + a.Vehicle.Model.Name + " (" + a.Vehicle.RegistrationPlate + ")",
                    string.Join(", ", a.Items.Select(i => i.Service.Name)),
                    a.Status,
                    a.TotalPrice))
                .ToListAsync(ct)
            : [];

        return new AppointmentReportResponse
        {
            From = range.From,
            To = range.To,
            GeneratedAtUtc = _clock.UtcNow,
            GeneratedBy = await GeneratedByAsync(ct),
            Currency = _currency,
            Total = new ReportFigure(total, Change(total, totalBefore)),
            Completed = completed,
            Cancelled = cancelled,
            CompletionRate = Percent(completed, completed + cancelled),
            AverageDurationMinutes = Math.Round((decimal)averageDuration, 0),
            AveragePrice = new ReportFigure(Math.Round(averagePrice, 2), Change(averagePrice, averagePriceBefore)),
            AverageRating = rating is null ? null : new ReportFigure(rating.Value, ratingBefore is null ? null : Change(rating.Value, ratingBefore.Value)),
            ByPeriod = await ByPeriodAsync(range, appointments, ct),
            ByStatus = [.. byStatus.OrderBy(s => s.Status)],
            ByCategory = byCategory
                .Select(c => new ReportShareRow(c.Name, c.Count, c.Amount, Percent(c.Count, itemCount)))
                .ToList(),
            ByEmployee = byEmployee
                .Select(e => new EmployeeRow(e.Name, e.Total, e.Completed, Percent(e.Completed, e.Closed)))
                .ToList(),
            CancellationReasons = reasons,
            Appointments = rows,
        };
    }

    private sealed record Range(DateOnly From, DateOnly To, DateTime Start, DateTime End, DateTime PreviousStart, bool Daily);

    private sealed record RevenueTotals(decimal Gross, decimal Refunded, int Count)
    {
        public decimal Net => Gross - Refunded;

        public decimal Average => Count == 0 ? 0 : Math.Round(Gross / Count, 2);
    }

    private Range Resolve(ReportRangeRequest request)
    {
        var today = _clock.TodayUtc;
        var from = request.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = request.To ?? today;

        if (to < from)
        {
            throw new ValidationException("to", "Datum „do\" ne može biti prije datuma „od\".");
        }

        var days = to.DayNumber - from.DayNumber + 1;
        if (days > MaxRangeDays)
        {
            throw new ValidationException("from", "Izvještaj može obuhvatiti najviše dvije godine.");
        }

        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new Range(from, to, start, end, start.AddDays(-days), days <= DailyUpToDays);
    }

    private async Task<RevenueTotals> RevenueTotalsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var paid = await _db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAtUtc >= from && p.PaidAtUtc < to)
            .GroupBy(_ => 1)
            .Select(g => new { Gross = g.Sum(p => p.AmountChargedStripe), Count = g.Count() })
            .SingleOrDefaultAsync(ct);

        var refunded = await _db.Set<Refund>()
            .Where(r => r.Status == RefundStatus.Succeeded && r.CompletedAtUtc >= from && r.CompletedAtUtc < to)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;

        return new RevenueTotals(paid?.Gross ?? 0, refunded, paid?.Count ?? 0);
    }

    private async Task<decimal?> RatingAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var average = await _db.Set<Review>()
            .Where(r => r.IsVisible && r.CreatedAtUtc >= from && r.CreatedAtUtc < to)
            .AverageAsync(r => (double?)r.Rating, ct);
        return average is null ? null : Math.Round((decimal)average.Value, 2);
    }

    // One row per day or month of the range, each with its net revenue and the appointments
    // scheduled in it; empty periods included, so the chart has no gaps.
    private async Task<IReadOnlyList<ReportPeriodRow>> ByPeriodAsync(
        Range range,
        IQueryable<Appointment> appointments,
        CancellationToken ct)
    {
        var payments = _db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAtUtc >= range.Start && p.PaidAtUtc < range.End);
        var refunds = _db.Set<Refund>()
            .Where(r => r.Status == RefundStatus.Succeeded && r.CompletedAtUtc >= range.Start && r.CompletedAtUtc < range.End);
        var booked = appointments.Where(a => a.ScheduledStartUtc >= range.Start && a.ScheduledStartUtc < range.End);

        Dictionary<DateOnly, decimal> charged, refunded;
        Dictionary<DateOnly, int> counts;

        if (range.Daily)
        {
            charged = (await payments
                    .GroupBy(p => p.PaidAtUtc!.Value.Date)
                    .Select(g => new { Day = g.Key, Amount = g.Sum(p => p.AmountChargedStripe) })
                    .ToListAsync(ct))
                .ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Amount);
            refunded = (await refunds
                    .GroupBy(r => r.CompletedAtUtc!.Value.Date)
                    .Select(g => new { Day = g.Key, Amount = g.Sum(r => r.Amount) })
                    .ToListAsync(ct))
                .ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Amount);
            counts = (await booked
                    .GroupBy(a => a.ScheduledStartUtc.Date)
                    .Select(g => new { Day = g.Key, Count = g.Count() })
                    .ToListAsync(ct))
                .ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        }
        else
        {
            charged = (await payments
                    .GroupBy(p => new { p.PaidAtUtc!.Value.Year, p.PaidAtUtc.Value.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.AmountChargedStripe) })
                    .ToListAsync(ct))
                .ToDictionary(x => new DateOnly(x.Year, x.Month, 1), x => x.Amount);
            refunded = (await refunds
                    .GroupBy(r => new { r.CompletedAtUtc!.Value.Year, r.CompletedAtUtc.Value.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(r => r.Amount) })
                    .ToListAsync(ct))
                .ToDictionary(x => new DateOnly(x.Year, x.Month, 1), x => x.Amount);
            counts = (await booked
                    .GroupBy(a => new { a.ScheduledStartUtc.Year, a.ScheduledStartUtc.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToListAsync(ct))
                .ToDictionary(x => new DateOnly(x.Year, x.Month, 1), x => x.Count);
        }

        var rows = new List<ReportPeriodRow>();
        var bucket = range.Daily ? range.From : new DateOnly(range.From.Year, range.From.Month, 1);
        while (bucket <= range.To)
        {
            rows.Add(new ReportPeriodRow(
                range.Daily
                    ? bucket.ToString("dd.MM.", CultureInfo.InvariantCulture)
                    : bucket.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                charged.GetValueOrDefault(bucket) - refunded.GetValueOrDefault(bucket),
                counts.GetValueOrDefault(bucket)));
            bucket = range.Daily ? bucket.AddDays(1) : bucket.AddMonths(1);
        }

        return rows;
    }

    private async Task<string> GeneratedByAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .SingleAsync(ct);
    }

    private static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(100m * part / whole, 1);

    private static decimal? Change(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round(100m * (current - previous) / previous, 1);
}
