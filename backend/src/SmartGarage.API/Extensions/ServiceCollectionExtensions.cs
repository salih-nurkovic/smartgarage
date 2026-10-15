using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartGarage.API.Consumers;
using SmartGarage.API.Filters;
using SmartGarage.API.Hubs;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Extensions;

/// <summary>
/// Everything the HTTP host registers, in one call from <c>Program.cs</c>. CORS, Swagger and
/// authentication are each configured here exactly once, which the instructions check for.
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "SmartGarageCors";

    private static readonly string[] UnreadableValueMessages = ["Vrijednost nije u ispravnom formatu."];

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = InvalidModelStateResponse);

        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();

        services.AddJwtAuthentication();
        services.AddRolePolicies();
        services.AddCorsPolicy(configuration);
        services.AddRealtime();

        if (configuration.GetValue("ENABLE_SWAGGER", false))
        {
            services.AddSwaggerWithBearer();
        }

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddScoped<SessionJwtBearerEvents>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Claims keep the names they were issued with, so AppClaims is the single
                // vocabulary on both sides.
                bearer.MapInboundClaims = false;

                // The API is deliberately HTTP only (instructions, 9.2).
                bearer.RequireHttpsMetadata = false;

                bearer.EventsType = typeof(SessionJwtBearerEvents);

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    // The default five minutes of tolerance would keep a 15-minute token
                    // alive for 20.
                    ClockSkew = TimeSpan.Zero,

                    NameClaimType = AppClaims.UserName,
                    RoleClaimType = AppClaims.Role,
                };
            });
    }

    private static void AddRealtime(this IServiceCollection services)
    {
        // Enums travel as names over the hub as well, the same as in REST responses.
        services
            .AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
        services.AddHostedService<RealtimeFanoutConsumer>();
    }

    // The only place a policy is tied to role names. Controllers name the policy.
    private static void AddRolePolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Administrator))
            .AddPolicy(Policies.StaffOnly, policy => policy.RequireRole(Roles.Administrator, Roles.Employee))
            .AddPolicy(Policies.CustomerOnly, policy => policy.RequireRole(Roles.Customer));

    private static void AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        }));
    }

    private static void AddSwaggerWithBearer(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartGarage API", Version = "v1" });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Unesite access token dobijen sa /api/auth/login.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme,
                },
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
        });

    /// <summary>
    /// A body that could not even be bound (malformed JSON, text where a number belongs) gets
    /// the same error shape as a validation failure. The binder's own messages are replaced
    /// because they quote .NET type names, which is internal detail a client should not see.
    /// </summary>
    private static BadRequestObjectResult InvalidModelStateResponse(ActionContext context)
    {
        // "$" and "$.field" are how the JSON binder names the body and its members.
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key.TrimStart('$', '.'))
            .Select(key => key.Length == 0 ? "body" : JsonNamingPolicy.CamelCase.ConvertName(key))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                key => key,
                _ => UnreadableValueMessages,
                StringComparer.Ordinal);

        return new BadRequestObjectResult(
            ApiErrorWriter.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Zahtjev nije moguće pročitati. Provjerite format poslanih podataka.",
                errors));
    }
}
