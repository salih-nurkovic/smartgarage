using SmartGarage.Application.Dtos.Reports;

namespace SmartGarage.Application.Abstractions;

/// <summary>
/// Turns a report into an A4 PDF. The figures are exactly the ones the JSON endpoint returns,
/// so what the desktop shows and what is printed cannot disagree.
/// </summary>
public interface IReportPdfRenderer
{
    byte[] Render(RevenueReportResponse report);

    byte[] Render(AppointmentReportResponse report);
}
