using System.Globalization;
using Serilog;
using Serilog.Events;
using SmartGarage.Application;
using SmartGarage.Infrastructure;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Worker.Extensions;

// ---------------------------------------------------------------------------
//  SmartGarage Worker - the supporting microservice
//  Student: Salih Nurkovic, IB220014
//
//  A SEPARATE project, Dockerfile, container and process. The instructions (3.2)
//  say outright that a BackgroundService inside the API does not count as a
//  second service.
//
//  The Worker talks to the API only through RabbitMQ and has no HTTP surface
//  besides its health port. Consumers and Quartz jobs are registered in
//  AddWorkerServices().
// ---------------------------------------------------------------------------

const int LogFilesKept = 7;

var envFile = DotEnvLoader.Load(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .MinimumLevel.Is(ParseLogLevel(builder.Configuration["LOG_LEVEL"]))
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Source", "Worker")
    .ReadFrom.Services(services)
    .WriteTo.Console(
        formatProvider: CultureInfo.InvariantCulture,
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "smartgarage-worker-.log"),
        formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: LogFilesKept,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// The same infrastructure, but with no signed-in user: the Worker handles messages, not
// HTTP requests. It never runs migrations - only the API does.
builder.Services.AddInfrastructureForWorker(builder.Configuration);

// The same service layer as the API: a scheduled job goes through the same rules as a
// person doing the same thing by hand (the state machine when cancelling, for one).
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddWorkerServices(builder.Configuration);

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation(
    "SmartGarage Worker started. Environment={Environment}, .env={EnvFile}",
    builder.Environment.EnvironmentName,
    envFile ?? "not found");

await host.RunAsync();

static LogEventLevel ParseLogLevel(string? value) =>
    Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
        ? level
        : LogEventLevel.Information;
