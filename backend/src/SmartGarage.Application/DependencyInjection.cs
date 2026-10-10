using System.Text;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Dtos.Reference;
using SmartGarage.Application.Options;
using SmartGarage.Application.Services;

namespace SmartGarage.Application;

/// <summary>
/// The single registration point for the service layer: services, validators and the options
/// they read. Called exactly once, by the API.
/// </summary>
public static class DependencyInjection
{
    private const int MinResetCodeLength = 4;
    private const int MaxResetCodeLength = 9;

    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bound once at startup. A missing or weak JWT key stops the application from
        // starting, rather than letting it run and sign tokens with something guessable.
        services.AddOptions<JwtOptions>()
            .Configure(options =>
            {
                options.Key = configuration["JWT_KEY"] ?? string.Empty;
                options.Issuer = configuration["JWT_ISSUER"] ?? string.Empty;
                options.Audience = configuration["JWT_AUDIENCE"] ?? string.Empty;
                options.AccessTokenMinutes = configuration.GetValue("JWT_ACCESS_TOKEN_MINUTES", 15);
                options.RefreshTokenDays = configuration.GetValue("JWT_REFRESH_TOKEN_DAYS", 14);
            })
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinimumKeyBytes,
                $"JWT_KEY must be at least {JwtOptions.MinimumKeyBytes} bytes long.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "JWT_ISSUER and JWT_AUDIENCE must be set.")
            .Validate(
                o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0,
                "JWT_ACCESS_TOKEN_MINUTES and JWT_REFRESH_TOKEN_DAYS must be positive.")
            .ValidateOnStart();

        services.AddOptions<PasswordResetOptions>()
            .Configure(options =>
            {
                options.CodeLength = configuration.GetValue("RESET_CODE_LENGTH", 6);
                options.CodeExpiryMinutes = configuration.GetValue("RESET_CODE_EXPIRY_MINUTES", 15);
            })
            .Validate(
                o => o.CodeLength is >= MinResetCodeLength and <= MaxResetCodeLength,
                $"RESET_CODE_LENGTH must be between {MinResetCodeLength} and {MaxResetCodeLength}.")
            .Validate(o => o.CodeExpiryMinutes > 0, "RESET_CODE_EXPIRY_MINUTES must be positive.")
            .ValidateOnStart();

        services.AddMemoryCache();

        // One message per field at a time: the first broken rule is the one worth showing.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Scoped, like the DbContext they use. The instructions name Transient here as a defect.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IErrorLogService, ErrorLogService>();

        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ILookupService, LookupService>();

        // The reference tables share one controller shape, so each is registered under the
        // generic contract that controller asks for.
        services.AddScoped<ICrudService<VehicleBrandResponse, ReferenceSearchRequest, VehicleBrandRequest>, VehicleBrandService>();
        services.AddScoped<ICrudService<VehicleModelResponse, VehicleModelSearchRequest, VehicleModelRequest>, VehicleModelService>();
        services.AddScoped<ICrudService<EngineTypeResponse, ReferenceSearchRequest, EngineTypeRequest>, EngineTypeService>();
        services.AddScoped<ICrudService<ServiceCategoryResponse, ReferenceSearchRequest, ServiceCategoryRequest>, ServiceCategoryService>();
        services.AddScoped<ICrudService<ServiceTagResponse, ReferenceSearchRequest, ServiceTagRequest>, ServiceTagService>();

        return services;
    }
}
