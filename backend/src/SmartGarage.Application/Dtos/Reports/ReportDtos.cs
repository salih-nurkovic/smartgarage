using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Dtos.Reports;

/// <summary>
/// The days a report covers, both included. Without dates it is the current month.
/// </summary>
public class ReportRangeRequest
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

public sealed class AppointmentReportRequest : ReportRangeRequest
{
    public AppointmentStatus? Status { get; init; }

    public int? EmployeeId { get; init; }
}

/// <summary>Common head of every report: what it covers, and who made it when.</summary>
public abstract class ReportHeader
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public DateTime GeneratedAtUtc { get; init; }

    public string GeneratedBy { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;
}

/// <summary>A figure with its change against the period of the same length just before.</summary>
public sealed record ReportFigure(decimal Value, decimal? ChangePercent);

/// <summary>
/// One bar group of the chart: a day for short ranges, a month for long ones.
/// </summary>
public sealed record ReportPeriodRow(string Label, decimal Revenue, int Appointments);

public sealed record ReportShareRow(string Name, int Count, decimal Amount, decimal Percent);

public sealed record RefundRow(string PaymentNumber, DateTime CompletedAtUtc, decimal Amount, string Reason, string RequestedBy);

public sealed class RevenueReportResponse : ReportHeader
{
    public required ReportFigure GrossRevenue { get; init; }

    public required decimal RefundedAmount { get; init; }

    public required ReportFigure NetRevenue { get; init; }

    public required int PaymentCount { get; init; }

    public required ReportFigure AverageTransaction { get; init; }

    public required IReadOnlyList<ReportPeriodRow> ByPeriod { get; init; }

    public required IReadOnlyList<ReportShareRow> ByCategory { get; init; }

    /// <summary>The fifteen services that earned most in the range.</summary>
    public required IReadOnlyList<ReportShareRow> TopServices { get; init; }

    public required IReadOnlyList<RefundRow> Refunds { get; init; }
}

public sealed record StatusCountRow(AppointmentStatus Status, int Count);

public sealed record EmployeeRow(string Name, int Total, int Completed, decimal CompletionRate);

public sealed record ReasonRow(string Reason, int Count);

public sealed record AppointmentRow(
    string AppointmentNumber,
    DateTime ScheduledStartUtc,
    string CustomerFullName,
    string VehicleDisplay,
    string Services,
    AppointmentStatus Status,
    decimal TotalPrice);

public sealed class AppointmentReportResponse : ReportHeader
{
    public required ReportFigure Total { get; init; }

    public required int Completed { get; init; }

    public required int Cancelled { get; init; }

    public required decimal CompletionRate { get; init; }

    public required decimal AverageDurationMinutes { get; init; }

    public required ReportFigure AveragePrice { get; init; }

    /// <summary>Average of the reviews written in the range, 1 to 5; null without reviews.</summary>
    public required ReportFigure? AverageRating { get; init; }

    public required IReadOnlyList<ReportPeriodRow> ByPeriod { get; init; }

    public required IReadOnlyList<StatusCountRow> ByStatus { get; init; }

    public required IReadOnlyList<ReportShareRow> ByCategory { get; init; }

    public required IReadOnlyList<EmployeeRow> ByEmployee { get; init; }

    public required IReadOnlyList<ReasonRow> CancellationReasons { get; init; }

    /// <summary>
    /// The appointments themselves, newest first, for the PDF. Capped so a report over a long
    /// range stays a document; <see cref="Total"/> says how many there are in all.
    /// </summary>
    public required IReadOnlyList<AppointmentRow> Appointments { get; init; }
}
