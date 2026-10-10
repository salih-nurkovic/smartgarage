using System.Globalization;
using Serilog;
using Serilog.Events;
using SmartGarage.Infrastructure;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Worker.Extensions;

// ---------------------------------------------------------------------------
//  SmartGarage Worker  -  pomocni mikroservis
//  Student: Salih Nurkovic, IB220014
//
//  Ovo je ZASEBAN projekat, zaseban Dockerfile, zaseban kontejner i zaseban
//  proces. Upute (3.2) izricito navode da BackgroundService unutar API projekta
//  NE zadovoljava uslov dva odvojena servisa.
//
//  Worker komunicira sa API-jem iskljucivo preko RabbitMQ-a. Nema HTTP povrsinu
//  osim health porta.
//
//  Consumeri se registruju kroz AddWorkerServices(); Quartz zadaci dolaze sa
//  fazama koje ih trebaju.
// ---------------------------------------------------------------------------

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
        retainedFileCountLimit: 7,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// Worker koristi istu infrastrukturu, ali bez prijavljenog korisnika: on obraduje
// poruke, ne HTTP zahtjeve. Migracije NE pokrece - to radi iskljucivo API.
builder.Services.AddInfrastructureForWorker(builder.Configuration);
builder.Services.AddWorkerServices();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation(
    "SmartGarage Worker pokrenut. Okruzenje={Environment}, .env={EnvFile}",
    builder.Environment.EnvironmentName,
    envFile ?? "nije pronaden");

await host.RunAsync();

static LogEventLevel ParseLogLevel(string? value) =>
    Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
        ? level
        : LogEventLevel.Information;
