using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Reports;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// The administration reports, as data for the desktop screen and as a PDF of the same figures.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class ReportsController : ControllerBase
{
    private const string PdfType = "application/pdf";

    private readonly IReportService _reports;
    private readonly IReportPdfRenderer _renderer;

    public ReportsController(IReportService reports, IReportPdfRenderer renderer)
    {
        _reports = reports;
        _renderer = renderer;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportResponse>> Revenue(
        [FromQuery] ReportRangeRequest request,
        CancellationToken ct) =>
        Ok(await _reports.GetRevenueAsync(request, ct));

    [HttpGet("revenue/pdf")]
    public async Task<IActionResult> RevenuePdf([FromQuery] ReportRangeRequest request, CancellationToken ct)
    {
        var report = await _reports.GetRevenueAsync(request, ct);
        return File(_renderer.Render(report), PdfType, FileName("prihodi", report));
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<AppointmentReportResponse>> Appointments(
        [FromQuery] AppointmentReportRequest request,
        CancellationToken ct) =>
        Ok(await _reports.GetAppointmentsAsync(request, includeRows: false, ct));

    [HttpGet("appointments/pdf")]
    public async Task<IActionResult> AppointmentsPdf([FromQuery] AppointmentReportRequest request, CancellationToken ct)
    {
        var report = await _reports.GetAppointmentsAsync(request, includeRows: true, ct);
        return File(_renderer.Render(report), PdfType, FileName("rezervacije", report));
    }

    private static string FileName(string name, ReportHeader report) =>
        $"izvjestaj-{name}-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.pdf";
}
