namespace SmartGarage.Application.Dtos.Dashboard;

/// <summary>
/// One headline number with its change. <see cref="ChangePercent"/> compares this month so far
/// with the same number of days of the previous month; it is null when there is nothing to
/// compare with.
/// </summary>
public sealed record KpiResponse(decimal Value, decimal? ChangePercent);

public sealed record MonthlyRevenueResponse(int Year, int Month, decimal Revenue);

public sealed record ServiceShareResponse(string Name, int Count, decimal Percent);

/// <summary>
/// The desktop dashboard. Money is shown to administrators only, so the revenue parts are null
/// for employees.
/// </summary>
public sealed class DashboardResponse
{
    public required KpiResponse Customers { get; init; }

    public required KpiResponse ActiveAppointments { get; init; }

    public required KpiResponse CompletedServices { get; init; }

    public KpiResponse? MonthRevenue { get; init; }

    public IReadOnlyList<MonthlyRevenueResponse>? RevenueByMonth { get; init; }

    /// <summary>Completed work by service category over the last six months.</summary>
    public required IReadOnlyList<ServiceShareResponse> ServiceDistribution { get; init; }
}
