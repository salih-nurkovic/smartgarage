using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Constants;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class LoginAndRegistrationTests
{
    private readonly HttpClient _client;

    public LoginAndRegistrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401WithTheStandardErrorBody()
    {
        var response = await _client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error!.Status.Should().Be(401);
        error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("desktop", Roles.Administrator)]
    [InlineData("mobile", Roles.Customer)]
    [InlineData("admin", Roles.Administrator)]
    [InlineData("employee", Roles.Employee)]
    [InlineData("customer", Roles.Customer)]
    public async Task Login_WithThePrescribedAccounts_SucceedsByUserNameAndPasswordTest(
        string userName,
        string expectedRole)
    {
        var auth = await _client.LoginAsync(userName);

        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.User.UserName.Should().Be(userName);
        auth.User.Roles.Should().Equal(expectedRole);

        var profile = await _client.GetAsync("/api/profile", auth.AccessToken);
        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profile.Content.ReadFromJsonAsync<UserResponse>())!.UserName.Should().Be(userName);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownUser_Returns401WithTheSameMessage()
    {
        var wrongPassword = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { UserName = "mobile", Password = "pogresna" });
        var unknownUser = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { UserName = "nepostojeci", Password = "pogresna" });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var first = await wrongPassword.Content.ReadFromJsonAsync<ApiErrorResponse>();
        var second = await unknownUser.Content.ReadFromJsonAsync<ApiErrorResponse>();

        // Identical text, so the response does not confirm which usernames exist.
        first!.Detail.Should().Be(second!.Detail);
    }

    [Fact]
    public async Task Login_WithCredentialsInTheQueryString_IsNotAccepted()
    {
        var response = await _client.PostAsync(
            "/api/auth/login?userName=mobile&password=test", content: null);

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Login_ForADeactivatedAccount_Returns403()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserName = "neaktivan", Password = ApiClientExtensions.SeedPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_AlwaysCreatesACustomer_EvenWhenTheBodyAsksForAnotherRole()
    {
        var userName = $"hacker{Guid.NewGuid():N}"[..20];

        // Sent as raw JSON on purpose: the request type has no role property to set.
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            userName,
            email = $"{userName}@example.com",
            firstName = "Zlo",
            lastName = "Namjeran",
            phoneNumber = "+387 61 999 999",
            password = ApiClientExtensions.StrongPassword,
            confirmPassword = ApiClientExtensions.StrongPassword,
            role = Roles.Administrator,
            roles = new[] { Roles.Administrator },
            isAdmin = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        auth.User.Roles.Should().Equal(Roles.Customer);

        var adminOnly = await _client.GetAsync("/api/users", auth.AccessToken);
        adminOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_Returns400WithTheMessageUnderThePasswordField()
    {
        var userName = $"slab{Guid.NewGuid():N}"[..20];

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 000 000",
            Password = "test",
            ConfirmPassword = "test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error!.Errors.Should().ContainKey("password");
    }

    [Fact]
    public async Task Register_WithATakenUserNameOrMalformedFields_Returns400KeyedByField()
    {
        var taken = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = "mobile",
            Email = "novi-mobile@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 000 000",
            Password = ApiClientExtensions.StrongPassword,
            ConfirmPassword = ApiClientExtensions.StrongPassword,
        });

        taken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await taken.Content.ReadFromJsonAsync<ApiErrorResponse>())!
            .Errors.Should().ContainKey("userName");

        var malformed = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = "x",
            Email = "nije-email",
            FirstName = string.Empty,
            LastName = "K",
            PhoneNumber = "abc",
            Password = ApiClientExtensions.StrongPassword,
            ConfirmPassword = "drugacija",
        });

        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await malformed.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Errors!.Keys.Should()
            .BeEquivalentTo("userName", "email", "firstName", "lastName", "phoneNumber", "confirmPassword");
    }
}
