using System.Globalization;
using Serilog;
using Serilog.Events;
using SmartGarage.Infrastructure;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Infrastructure.Persistence.Seeding;

// ---------------------------------------------------------------------------
//  SmartGarage REST API
//  Student: Salih Nurkovic, IB220014
//
//  Faza 1: kostur hosta. Servisi, autentifikacija, baza i kontroleri dolaze u
//  fazama 2-9, svaki put kroz JEDNU extension metodu, tako da ovdje nikada ne
//  nastane duplirana registracija (upute, sekcija 3.4).
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
// Jedan poziv, jedno mjesto. Sve iz Infrastructure sloja (DbContext, Identity,
// seederi) registruje se ovdje i nigdje drugdje.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// CORS se konfigurise TACNO JEDNOM, sa eksplicitno navedenim origin-ima.
const string CorsPolicyName = "SmartGarageCors";
var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

var swaggerEnabled = builder.Configuration.GetValue("ENABLE_SWAGGER", false);
if (swaggerEnabled)
{
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
app.UseSerilogRequestLogging();

// NAMJERNO nema UseHttpsRedirection(): upute (9.2) preporucuju http pristup API-ju
// jer self-signed certifikati mogu isteci ili biti nevalidni tokom pregleda rada.

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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
