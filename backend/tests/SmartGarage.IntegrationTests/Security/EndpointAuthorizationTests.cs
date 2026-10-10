using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Domain.Constants;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Security;

/// <summary>
/// Reads the routing table of the running application and checks who may call what.
/// <para>
/// This guards against the mistake the instructions warn about most often: an endpoint left
/// open by accident. Adding an anonymous endpoint makes this test fail until it is added to
/// the list below, which turns "is this one meant to be public?" into a deliberate decision.
/// </para>
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class EndpointAuthorizationTests
{
    private static readonly string[] IntentionallyAnonymous =
    [
        "POST api/auth/register",
        "POST api/auth/login",
        "POST api/auth/refresh",
        "POST api/auth/forgot-password",
        "POST api/auth/reset-password",
        "GET /health",
        "GET /health/ready",
    ];

    private readonly IReadOnlyList<RouteEndpoint> _endpoints;

    public EndpointAuthorizationTests(ApiFactory factory)
    {
        _endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void OnlyTheListedEndpointsAreAnonymous()
    {
        var anonymous = _endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(Describe)
            .ToList();

        anonymous.Should().BeEquivalentTo(IntentionallyAnonymous);
    }

    [Fact]
    public void EveryOtherEndpointRequiresAnAuthenticatedUser()
    {
        var unprotected = _endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .SelectMany(Describe)
            .ToList();

        unprotected.Should().BeEmpty();
    }

    [Fact]
    public void EveryUserManagementEndpointDemandsTheAdministratorPolicy()
    {
        var userEndpoints = _endpoints
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/users", StringComparison.Ordinal))
            .ToList();

        userEndpoints.Should().NotBeEmpty();
        userEndpoints.Should().OnlyContain(e => e.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(a => a.Policy == Policies.AdminOnly));
    }

    private static IEnumerable<string> Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
        return methods.Select(method => $"{method} {endpoint.RoutePattern.RawText}");
    }
}
