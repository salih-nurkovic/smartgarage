using System.Globalization;
using Serilog;
using Serilog.Events;
using SmartGarage.API.Extensions;
using SmartGarage.API.Hubs;
using SmartGarage.API.Middleware;
using SmartGarage.Application;
using SmartGarage.Infrastructure;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Infrastructure.Persistence.Seeding;

// ---------------------------------------------------------------------------
//  SmartGarage REST API
//  Student: Salih Nurkovic, IB220014
//
//  Only the host is assembled here. Each layer registers itself through ONE
//  extension method, so nothing can be registered twice (instructions, 3.4).
// ---------------------------------------------------------------------------

const int LogFilesKept = 7;

// .env is loaded before anything else: it is the only source of configuration.
var envFile = DotEnvLoader.Load(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---------------------------------------------------------------
// The code depends on ILogger<T> only; Serilog is the implementation behind it.
// Enough context to reproduce a problem (instructions, 3.2).
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
        retainedFileCountLimit: LogFilesKept,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// --- Services --------------------------------------------------------------
// One call per layer. Every registration lives in these extension methods and
// nowhere else.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
app.UseSerilogRequestLogging();

// First, so that it catches an exception from any part of the pipeline after it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Deliberately no UseHttpsRedirection(): the instructions (9.2) recommend plain HTTP,
// because a self-signed certificate may expire or be rejected while the work is graded.

var swaggerEnabled = builder.Configuration.GetValue("ENABLE_SWAGGER", false);
if (swaggerEnabled)
{
    app.UseSwagger();
    // A token entered once survives a page reload.
    app.UseSwaggerUI(options => options.EnablePersistAuthorization());
}

// Deliberately no UseStaticFiles(): images are read only through endpoints that check
// sign-in and ownership (instructions, 5), never straight from disk.
app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Every controller requires sign-in unless an action is explicitly [AllowAnonymous],
// so a forgotten attribute leaves an endpoint locked rather than open.
app.MapControllers().RequireAuthorization();
app.MapHub<NotificationHub>(NotificationHub.Path).RequireAuthorization();

// Docker Compose uses these in its healthcheck, so the Worker does not start before
// the API has applied the migrations.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Migrations and seed data. Only the API does this; the Worker waits for the API to be
// healthy, so two processes never migrate the same database at once.
await DatabaseInitializer.InitializeAsync(app.Services);

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "SmartGarage API started. Environment={Environment}, .env={EnvFile}, Swagger={Swagger}",
    app.Environment.EnvironmentName,
    envFile ?? "not found",
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
