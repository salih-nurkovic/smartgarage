using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Reports;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Reporting;

/// <summary>
/// The two reports as A4 documents: a head saying what the report covers and who made it, the
/// sections, and a numbered foot on every page. Times are shown as stored, in service time,
/// like everywhere else in the system.
/// </summary>
public sealed class QuestPdfReportRenderer : IReportPdfRenderer
{
    private const string Accent = "#1E5AA8";
    private const string Muted = "#6B7280";
    private const string Rule = "#D1D5DB";
    private const string Shade = "#F3F4F6";

    // Fixed separators instead of a culture, so the output does not depend on which cultures
    // the container's ICU data happens to include.
    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    private readonly string _garageName;

    static QuestPdfReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public QuestPdfReportRenderer(IOptions<GarageOptions> garage)
    {
        _garageName = garage.Value.Name;
    }

    public byte[] Render(RevenueReportResponse report) =>
        Build("Izvještaj o prihodima", report, landscape: false, column =>
        {
            Summary(column, [
                ("Bruto prihod", Money(report.GrossRevenue.Value, report.Currency), report.GrossRevenue.ChangePercent),
                ("Povrati", Money(report.RefundedAmount, report.Currency), null),
                ("Neto prihod", Money(report.NetRevenue.Value, report.Currency), report.NetRevenue.ChangePercent),
                ("Broj uplata", report.PaymentCount.ToString(CultureInfo.InvariantCulture), null),
                ("Prosječna uplata", Money(report.AverageTransaction.Value, report.Currency), report.AverageTransaction.ChangePercent),
            ]);

            Table(column, "Prihod po periodu", ["Period", "Rezervacija", $"Neto prihod ({report.Currency})"], [3, 2, 3], 1,
                report.ByPeriod.Select(p => new[] { p.Label, Count(p.Appointments), Amount(p.Revenue) }),
                ["Ukupno", Count(report.ByPeriod.Sum(p => p.Appointments)), Amount(report.ByPeriod.Sum(p => p.Revenue))]);

            Table(column, "Prihod po kategoriji usluga", ["Kategorija", "Stavki", $"Iznos ({report.Currency})", "Udio"], [4, 1.5f, 2, 1.5f], 1,
                report.ByCategory.Select(c => new[] { c.Name, Count(c.Count), Amount(c.Amount), Percent(c.Percent) }),
                ["Ukupno", Count(report.ByCategory.Sum(c => c.Count)), Amount(report.ByCategory.Sum(c => c.Amount)), report.ByCategory.Count == 0 ? "-" : "100 %"]);

            Table(column, "Najprofitabilnije usluge (do 15)", ["#", "Usluga", "Stavki", $"Iznos ({report.Currency})", "Udio"], [0.6f, 5, 1.5f, 2, 1.5f], 2,
                report.TopServices.Select((s, i) => new[] { Count(i + 1), s.Name, Count(s.Count), Amount(s.Amount), Percent(s.Percent) }),
                null);

            Table(column, "Povrati", ["Plaćanje", "Datum", "Razlog", "Zatražio", $"Iznos ({report.Currency})"], [2.2f, 2, 4, 2.5f, 1.8f], 4,
                report.Refunds.Select(r => new[] { r.PaymentNumber, DateTimeText(r.CompletedAtUtc), r.Reason, r.RequestedBy, Amount(r.Amount) }),
                ["Ukupno", string.Empty, string.Empty, string.Empty, Amount(report.RefundedAmount)]);
        });

    public byte[] Render(AppointmentReportResponse report) =>
        Build("Izvještaj o rezervacijama", report, landscape: true, column =>
        {
            Summary(column, [
                ("Ukupno rezervacija", Count((int)report.Total.Value), report.Total.ChangePercent),
                ("Završeno", Count(report.Completed), null),
                ("Otkazano", Count(report.Cancelled), null),
                ("Stopa završenosti", Percent(report.CompletionRate), null),
                ("Prosječno trajanje", $"{Count((int)report.AverageDurationMinutes)} min", null),
                ("Prosječna cijena", Money(report.AveragePrice.Value, report.Currency), report.AveragePrice.ChangePercent),
                ("Prosječna ocjena", report.AverageRating is null ? "-" : $"{report.AverageRating.Value.ToString("0.00", Numbers)} / 5", report.AverageRating?.ChangePercent),
            ]);

            var total = report.ByStatus.Sum(s => s.Count);
            Table(column, "Po statusu", ["Status", "Broj", "Udio"], [4, 2, 2], 1,
                report.ByStatus.Select(s => new[] { StatusLabel(s.Status), Count(s.Count), Percent(total == 0 ? 0 : Math.Round(100m * s.Count / total, 1)) }),
                ["Ukupno", Count(total), total == 0 ? "-" : "100 %"]);

            Table(column, "Po zaposleniku", ["Zaposlenik", "Dodijeljeno", "Završeno", "Stopa završenosti"], [4, 2, 2, 2], 1,
                report.ByEmployee.Select(e => new[] { e.Name, Count(e.Total), Count(e.Completed), Percent(e.CompletionRate) }),
                ["Ukupno", Count(report.ByEmployee.Sum(e => e.Total)), Count(report.ByEmployee.Sum(e => e.Completed)), string.Empty]);

            Table(column, "Razlozi otkazivanja (do 10 najčešćih)", ["Razlog", "Broj"], [6, 1.5f], 1,
                report.CancellationReasons.Select(r => new[] { r.Reason, Count(r.Count) }),
                null);

            var listed = report.Appointments.Count < total
                ? $"Rezervacije (najnovijih {report.Appointments.Count} od {total})"
                : "Rezervacije";
            Table(column, listed, ["Broj", "Termin", "Kupac", "Vozilo", "Usluge", "Status", $"Cijena ({report.Currency})"], [2.6f, 2.6f, 2.6f, 3.4f, 5, 1.6f, 1.6f], 6,
                report.Appointments.Select(a => new[]
                {
                    a.AppointmentNumber, DateTimeText(a.ScheduledStartUtc), a.CustomerFullName, a.VehicleDisplay,
                    a.Services, StatusLabel(a.Status), Amount(a.TotalPrice),
                }),
                null);
        });

    // The appointment report lies on its side: its list has seven columns, and an appointment
    // number broken over two lines is no longer something one can search for.
    private byte[] Build(string title, ReportHeader header, bool landscape, Action<ColumnDescriptor> body) =>
        Document.Create(document => document.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(9));

                page.Header().PaddingBottom(10).Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(_garageName).FontSize(10).FontColor(Muted);
                        row.RelativeItem().AlignRight().Text($"Generisano: {DateTimeText(header.GeneratedAtUtc)}").FontColor(Muted);
                    });
                    column.Item().Text(title).FontSize(18).Bold().FontColor(Accent);
                    column.Item().Text($"Period: {DateText(header.From)} – {DateText(header.To)}");
                    column.Item().Text($"Izradio: {header.GeneratedBy}").FontColor(Muted);
                    column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Rule);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    body(column);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontColor(Muted));
                    text.Span("Stranica ");
                    text.CurrentPageNumber();
                    text.Span(" od ");
                    text.TotalPages();
                });
            }))
            .GeneratePdf();

    private static void Summary(ColumnDescriptor column, IReadOnlyList<(string Label, string Value, decimal? Change)> figures)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                columns.RelativeColumn(3);
            });

            foreach (var (label, value, change) in figures)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).Text(label);
                table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).AlignRight().Text(value).Bold();
                table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).AlignRight()
                    .Text(change is null ? string.Empty : $"{Signed(change.Value)} prema prethodnom periodu")
                    .FontColor(Muted);
            }
        });
    }

    /// <summary>
    /// A table whose header repeats on every page it runs onto, with an optional totals row.
    /// The first <paramref name="textColumns"/> columns are text; the rest are figures and are
    /// aligned right so their digits line up. The title moves to the next page with the table
    /// when there is no room for its first rows.
    /// </summary>
    private static void Table(
        ColumnDescriptor column,
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<float> widths,
        int textColumns,
        IEnumerable<string[]> rows,
        string[]? totals)
    {
        var data = rows.ToList();

        column.Item().EnsureSpace(90).Column(section =>
        {
            section.Item().PaddingTop(10).PaddingBottom(4).Text(title).FontSize(12).Bold().FontColor(Accent);

            if (data.Count == 0)
            {
                section.Item().Text("Nema podataka za izabrani period.").Italic().FontColor(Muted);
                return;
            }

            section.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var width in widths)
                    {
                        columns.RelativeColumn(width);
                    }
                });

                table.Header(head =>
                {
                    for (var i = 0; i < headers.Count; i++)
                    {
                        Align(head.Cell().Background(Shade).Padding(3), i, textColumns).Text(headers[i]).Bold();
                    }
                });

                foreach (var row in data)
                {
                    for (var i = 0; i < row.Length; i++)
                    {
                        // Each cell is kept whole, so a row is never split across two pages.
                        Align(table.Cell().BorderBottom(0.5f).BorderColor(Rule).Padding(3).ShowEntire(), i, textColumns)
                            .Text(row[i]);
                    }
                }

                if (totals is not null)
                {
                    for (var i = 0; i < totals.Length; i++)
                    {
                        Align(table.Cell().BorderTop(1).BorderColor(Muted).Padding(3), i, textColumns).Text(totals[i]).Bold();
                    }
                }
            });
        });
    }

    private static IContainer Align(IContainer cell, int index, int textColumns) =>
        index < textColumns ? cell : cell.AlignRight();

    private static string StatusLabel(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Na čekanju",
        AppointmentStatus.Confirmed => "Potvrđeno",
        AppointmentStatus.InProgress => "U toku",
        AppointmentStatus.Completed => "Završeno",
        AppointmentStatus.Cancelled => "Otkazano",
        _ => status.ToString(),
    };

    private static string Amount(decimal value) => value.ToString("N2", Numbers);

    private static string Money(decimal value, string currency) => $"{Amount(value)} {currency}";

    private static string Count(int value) => value.ToString("N0", Numbers);

    private static string Percent(decimal value) => $"{value.ToString("0.0", Numbers)} %";

    private static string Signed(decimal value) => $"{(value > 0 ? "+" : string.Empty)}{Percent(value)}";

    private static string DateText(DateOnly value) => value.ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture);

    private static string DateTimeText(DateTime value) => value.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture);
}
