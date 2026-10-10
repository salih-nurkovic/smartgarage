using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Domain.Constants;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class PasswordResetTests
{
    private const string NewPassword = "Resetovana!789";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ResetPassword_WithTheEmailedCode_ChangesThePasswordAndEndsOldSessions()
    {
        var (session, userName) = await _client.RegisterNewCustomerAsync();
        var email = session.User.Email;

        await RequestCodeAsync(userName);
        var code = _factory.Events.LatestResetCodeFor(email);

        code.Should().MatchRegex("^[0-9]{6}$");

        var reset = await ResetAsync(userName, code);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.LoginAsync(userName, NewPassword)).AccessToken.Should().NotBeNullOrWhiteSpace();

        (await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest { UserName = userName, Password = ApiClientExtensions.StrongPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _client.GetAsync("/api/profile", session.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A code works once.
        (await ResetAsync(userName, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetCode_IsStoredHashedWithAnExpiry_NeverInPlainText()
    {
        var (session, userName) = await _client.RegisterNewCustomerAsync();

        await RequestCodeAsync(userName);
        var code = _factory.Events.LatestResetCodeFor(session.User.Email);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

        var stored = await db.PasswordResetCodes.SingleAsync(c => c.UserId == session.User.Id);

        stored.CodeHash.Should().NotContain(code);
        stored.ExpiresAtUtc.Should().BeAfter(stored.CreatedAtUtc);
    }

    [Fact]
    public async Task ResetPassword_AfterTheCodeExpires_IsRefused()
    {
        var (session, userName) = await _client.RegisterNewCustomerAsync();

        await RequestCodeAsync(userName);
        var code = _factory.Events.LatestResetCodeFor(session.User.Email);

        try
        {
            _factory.Clock.Offset = TimeSpan.FromMinutes(16);

            var reset = await ResetAsync(userName, code);

            reset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await reset.Content.ReadFromJsonAsync<ApiErrorResponse>())!
                .Errors.Should().ContainKey("code");
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task ResetPassword_AfterTooManyWrongGuesses_RefusesEvenTheRightCode()
    {
        var (session, userName) = await _client.RegisterNewCustomerAsync();

        await RequestCodeAsync(userName);
        var code = _factory.Events.LatestResetCodeFor(session.User.Email);
        var wrongCode = code == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < SecurityRules.ResetCodeMaxFailedAttempts; attempt++)
        {
            (await ResetAsync(userName, wrongCode)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await ResetAsync(userName, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_ForAnUnknownAccount_Returns200AndSendsNothing()
    {
        var sentBefore = _factory.Events.Published.Count;

        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { UserNameOrEmail = "nepostojeci-korisnik" });

        // Same answer as for a real account, so the endpoint cannot be used to list users.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Events.Published.Should().HaveCount(sentBefore);
    }

    [Fact]
    public async Task ForgotPassword_RequestedTwice_LeavesOnlyTheNewestCodeUsable()
    {
        var (session, userName) = await _client.RegisterNewCustomerAsync();

        await RequestCodeAsync(userName);
        var firstCode = _factory.Events.LatestResetCodeFor(session.User.Email);

        await RequestCodeAsync(session.User.Email);
        var secondCode = _factory.Events.LatestResetCodeFor(session.User.Email);

        if (firstCode != secondCode)
        {
            (await ResetAsync(userName, firstCode)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await ResetAsync(userName, secondCode)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task RequestCodeAsync(string userNameOrEmail)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { UserNameOrEmail = userNameOrEmail });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> ResetAsync(string userName, string code) =>
        _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            UserNameOrEmail = userName,
            Code = code,
            NewPassword = NewPassword,
            ConfirmNewPassword = NewPassword,
        });
}
