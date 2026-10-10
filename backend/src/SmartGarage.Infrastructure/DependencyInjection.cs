using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Entities;
using SmartGarage.Infrastructure.Email;
using SmartGarage.Infrastructure.Identity;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.Infrastructure.Persistence.Interceptors;
using SmartGarage.Infrastructure.Persistence.Seeding;
using SmartGarage.Infrastructure.Storage;

namespace SmartGarage.Infrastructure;

/// <summary>
/// The single registration point for everything Infrastructure provides.
/// <para>
/// Both hosts call one of these methods exactly once. Having one entry point is what prevents
/// the duplicate registrations the instructions call out, and it keeps <c>Program.cs</c> from
/// growing into a list of wiring that nobody can audit.
/// </para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and Identity for the API, where there is a signed-in user.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Stateless and built around one signing key, so a single instance serves every request.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordResetCodeSender, DevelopmentPasswordResetCodeSender>();

        // Holds only two values read from configuration at construction, so one instance is enough.
        services.AddSingleton<IImageStore, LocalImageStore>();

        return services.AddInfrastructureCore(configuration);
    }

    /// <summary>
    /// Registers persistence for the Worker, which processes messages and has no signed-in
    /// user. Everything else is identical, so the two hosts cannot drift apart.
    /// </summary>
    public static IServiceCollection AddInfrastructureForWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ICurrentUserService, SystemUserService>();

        return services.AddInfrastructureCore(configuration);
    }

    private static IServiceCollection AddInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<AuditingInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();

        // Scoped, never Singleton and never Transient. The instructions are explicit, and a
        // DbContext with any other lifetime produces concurrency bugs that only appear under
        // load - exactly when they are hardest to diagnose.
        services.AddDbContext<SmartGarageDbContext>((provider, options) =>
        {
            options.UseSqlServer(
                ConnectionStringBuilder.Build(configuration),
                sql =>
                {
                    sql.MigrationsAssembly(typeof(SmartGarageDbContext).Assembly.FullName);

                    // SQL Server in a container is not ready the instant it accepts a socket.
                    // Retrying here is what lets `docker compose up` succeed on a cold start
                    // without anyone having to run it twice.
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

            options.AddInterceptors(
                provider.GetRequiredService<AuditingInterceptor>(),
                provider.GetRequiredService<SoftDeleteInterceptor>());
        });

        // Services see the context only through this interface, and get the very same scoped
        // instance Identity's stores use, so one request has one unit of work.
        services.AddScoped<IAppDbContext>(
            provider => provider.GetRequiredService<SmartGarageDbContext>());

        // Seeders run once at startup from DatabaseInitializer. Registered here so they can
        // take the same DbContext, configuration and logger as everything else.
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<CatalogueSeeder>();
        services.AddScoped<DemoDataSeeder>();

        // AddIdentityCore, unlike the full AddIdentity, does not pull Data Protection in, yet
        // the default token providers below (password reset tokens) cannot be built without it.
        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Password policy comes from .env so it can be tightened or relaxed without
                // touching code. The seeded accounts prescribed by the instructions bypass
                // these rules deliberately - see IdentitySeeder.
                options.Password.RequiredLength =
                    configuration.GetValue("PASSWORD_MIN_LENGTH", 8);
                options.Password.RequireDigit =
                    configuration.GetValue("PASSWORD_REQUIRE_DIGIT", true);
                options.Password.RequireUppercase =
                    configuration.GetValue("PASSWORD_REQUIRE_UPPERCASE", true);
                options.Password.RequireLowercase =
                    configuration.GetValue("PASSWORD_REQUIRE_LOWERCASE", true);
                options.Password.RequireNonAlphanumeric =
                    configuration.GetValue("PASSWORD_REQUIRE_NONALPHANUMERIC", true);

                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<SmartGarageDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
