using System.Globalization;
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

        services.AddOptions<BusinessHoursOptions>()
            .Configure(options =>
            {
                options.WorkdayStart = ParseTime(configuration["WORKDAY_START"], new TimeOnly(8, 0));
                options.WorkdayEnd = ParseTime(configuration["WORKDAY_END"], new TimeOnly(17, 0));
            })
            .Validate(o => o.WorkdayEnd > o.WorkdayStart, "WORKDAY_END must be later than WORKDAY_START.")
            .ValidateOnStart();

        services.AddOptions<BookingOptions>()
            .Configure(options =>
            {
                options.MaxConcurrent = configuration.GetValue("APPOINTMENT_MAX_CONCURRENT", 5);
                options.SlotMinutes = configuration.GetValue("APPOINTMENT_SLOT_MINUTES", 60);
                options.BufferMinutes = configuration.GetValue("APPOINTMENT_BUFFER_MINUTES", 15);
                options.CancellationCutoffHours = configuration.GetValue("APPOINTMENT_CANCELLATION_CUTOFF_HOURS", 24);
                options.MaxDaysAhead = configuration.GetValue("BOOKING_MAX_DAYS_AHEAD", 90);
                options.ReminderHoursBefore = configuration.GetValue("APPOINTMENT_REMINDER_HOURS_BEFORE", 24);
            })
            .Validate(
                o => o.MaxConcurrent > 0 && o.SlotMinutes > 0 && o.MaxDaysAhead > 0 && o.BufferMinutes >= 0,
                "The APPOINTMENT_* and BOOKING_* settings must be positive.")
            .ValidateOnStart();

        services.AddOptions<PaymentOptions>()
            .Configure(options => options.StripeCurrency = configuration["STRIPE_CURRENCY"] ?? "eur");

        services.AddOptions<GarageOptions>()
            .Configure(options =>
            {
                options.Name = configuration["GARAGE_NAME"] ?? string.Empty;
                options.Address = configuration["GARAGE_ADDRESS"] ?? string.Empty;
                options.Phone = configuration["GARAGE_PHONE"] ?? string.Empty;
                options.DisplayCurrency = configuration["DISPLAY_CURRENCY"] ?? "EUR";
            })
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Name) && !string.IsNullOrWhiteSpace(o.Address),
                "GARAGE_NAME and GARAGE_ADDRESS must be set.")
            .ValidateOnStart();

        services.AddOptions<RecommenderOptions>()
            .Configure(options =>
            {
                options.TopN = configuration.GetValue("RECOMMENDER_TOP_N", 5);
                options.AlgorithmVersion = configuration["RECOMMENDER_ALGORITHM_VERSION"] ?? "cbf-1.0";
                options.SignalHalfLifeDays = Double(configuration, "SIGNAL_HALF_LIFE_DAYS", 180);
                options.SignalSaturationCount = configuration.GetValue("SIGNAL_SATURATION_COUNT", 8);
                options.HistoryBlendBeta = Double(configuration, "HISTORY_BLEND_BETA", 0.7);
                options.HighMileageThresholdKm = configuration.GetValue("HIGH_MILEAGE_THRESHOLD_KM", 150_000);
                options.OldVehicleAgeYears = configuration.GetValue("OLD_VEHICLE_AGE_YEARS", 10);
                options.EngineWeight = Double(configuration, "W_ENGINE", 0.20);
                options.DueWeight = Double(configuration, "W_MILEAGE", 0.30);
                options.AgeWeight = Double(configuration, "W_AGE", 0.10);
                options.RatingWeight = Double(configuration, "W_RATING", 0.10);
                options.BrandModelWeight = Double(configuration, "W_BRAND_MODEL", 0.15);
                options.RecentPenalty = Double(configuration, "W_RECENT", 0.50);
                options.ExplanationMinContribution = Double(configuration, "EXPLANATION_MIN_CONTRIBUTION", 0.05);
                options.UrgentThreshold = Double(configuration, "URGENCY_HITNO_THRESHOLD", 1.0);
            })
            .Validate(
                o => o.TopN > 0 && o.SignalHalfLifeDays > 0 && o.SignalSaturationCount > 0
                     && o.HistoryBlendBeta is >= 0 and <= 1,
                "The recommender settings in .env are out of range.")
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
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISlotPlanner, SlotPlanner>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRecommendationService, RecommendationService>();

        // The reference tables share one controller shape, so each is registered under the
        // generic contract that controller asks for.
        services.AddScoped<ICrudService<VehicleBrandResponse, ReferenceSearchRequest, VehicleBrandRequest>, VehicleBrandService>();
        services.AddScoped<ICrudService<VehicleModelResponse, VehicleModelSearchRequest, VehicleModelRequest>, VehicleModelService>();
        services.AddScoped<ICrudService<EngineTypeResponse, ReferenceSearchRequest, EngineTypeRequest>, EngineTypeService>();
        services.AddScoped<ICrudService<ServiceCategoryResponse, ReferenceSearchRequest, ServiceCategoryRequest>, ServiceCategoryService>();
        services.AddScoped<ICrudService<ServiceTagResponse, ReferenceSearchRequest, ServiceTagRequest>, ServiceTagService>();

        return services;
    }

    // Decimal settings are written with a dot in .env whatever the machine's language is.
    private static double Double(IConfiguration configuration, string key, double fallback) =>
        double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static TimeOnly ParseTime(string? value, TimeOnly fallback) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : fallback;
}
