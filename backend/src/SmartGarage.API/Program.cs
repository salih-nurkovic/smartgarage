using System.Globalization;
using Serilog;
using Serilog.Events;
using SmartGarage.API.Extensions;
using SmartGarage.API.Middleware;
using SmartGarage.Application;
using SmartGarage.Infrastructure;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Infrastructure.Persistence.Seeding;

// ---------------------------------------------------------------------------
//  SmartGarage REST API
//  Student: Salih Nurkovic, IB220014
//
//  Ovdje se samo sklapa host. Svaki sloj se registruje kroz JEDNU extension
//  metodu, tako da nikada ne nastane duplirana registracija (upute, sekcija 3.4).
// ---------------------------------------------------------------------------

// .env se ucitava prije svega ostalog: on je jedini izvor konfiguracije.
var envFile = DotEnvLoader.Load(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// --- Logiranje -------------------------------------------------------------
// ILogger<T> svuda u kodu; Serilog je samo implementacija iza njega.
// Dovoljno informacija za reprodukciju problema (upute, sekcija 3.2).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Is(ParseLogLevel(context.Configuration["LOG_LEVEL"]))
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Source", "API")
    .ReadFrom.Services(services)
    .WriteTo.Console(
        formatProvider: CultureInfo.InvariantCulture,
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "smartgarage-api-.log"),
        formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// --- Servisi ---------------------------------------------------------------
// Tri poziva, po jedan za svaki sloj. Sve registracije zive u tim extension
// metodama i nigdje drugdje, pa se nista ne moze registrovati dvaput.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
app.UseSerilogRequestLogging();

// Prvi u nizu, da uhvati izuzetak iz bilo kojeg dijela pipeline-a iza sebe.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// NAMJERNO nema UseHttpsRedirection(): upute (9.2) preporucuju http pristup API-ju
// jer self-signed certifikati mogu isteci ili biti nevalidni tokom pregleda rada.

var swaggerEnabled = builder.Configuration.GetValue("ENABLE_SWAGGER", false);
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// NAMJERNO nema UseStaticFiles(): slike se citaju iskljucivo kroz endpointe koji
// provjeravaju prijavu i vlasnistvo (upute, sekcija 5), nikada direktno sa diska.
app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Svaki kontroler trazi prijavu, osim akcija koje su izricito [AllowAnonymous].
// Zaboravljen atribut zato znaci zakljucan endpoint, a ne otvoren.
app.MapControllers().RequireAuthorization();

// Health endpoint-i: Docker Compose ih koristi u depends_on / healthcheck,
// pa Worker ne krece prije nego API primijeni migracije.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Migracije i seed podaci. API je jedini koji ovo radi; Worker ceka da API
// postane zdrav, pa dva procesa nikada ne migriraju istu bazu istovremeno.
await DatabaseInitializer.InitializeAsync(app.Services);

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "SmartGarage API pokrenut. Okruzenje={Environment}, .env={EnvFile}, Swagger={Swagger}",
    app.Environment.EnvironmentName,
    envFile ?? "nije pronaden",
    swaggerEnabled);

await app.RunAsync();

static LogEventLevel ParseLogLevel(string? value) =>
    Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
        ? level
        : LogEventLevel.Information;

/// <summary>
/// Exposed so that <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test project
/// can boot this host. Required because top-level statements generate an internal entry point.
/// </summary>
public partial class Program;
