using System.Net.Http.Json;
using FluentAssertions;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Contracts.Events;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Messaging;

/// <summary>
/// What the API hands to the broker. The Worker's side of the exchange is exercised against a
/// real RabbitMQ and SMTP server, not here.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PublishedEventTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PublishedEventTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Registration_PublishesAWelcomeEvent_CarryingWhatTheEmailNeeds()
    {
        var (auth, userName) = await _client.RegisterNewCustomerAsync();

        var published = _factory.Events.Published
            .OfType<UserRegisteredEvent>()
            .Single(e => e.UserName == userName);

        published.Email.Should().Be(auth.User.Email);
        published.FirstName.Should().Be(auth.User.FirstName);
        published.EventId.Should().NotBeEmpty();
        published.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FailedRegistration_PublishesNothing()
    {
        var before = _factory.Events.Published.Count;

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = "mobile",
            Email = "zauzeto@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 000 000",
            Password = ApiClientExtensions.StrongPassword,
            ConfirmPassword = ApiClientExtensions.StrongPassword,
        });

        _factory.Events.Published.Should().HaveCount(before);
    }
}
