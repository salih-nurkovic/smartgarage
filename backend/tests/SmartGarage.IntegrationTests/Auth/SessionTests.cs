using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Auth;

/// <summary>
/// Logout, refresh rotation and token integrity: the behaviours that only exist because the
/// server keeps track of sessions instead of trusting any token with a valid signature.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SessionTests
{
    private readonly HttpClient _client;

    public SessionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Logout_MakesTheAccessTokenFailImmediately_AndKillsItsRefreshToken()
    {
        var (auth, _) = await _client.RegisterNewCustomerAsync();

        (await _client.GetAsync("/api/profile", auth.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var logout = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout").WithBearer(auth.AccessToken));
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The token is unexpired and correctly signed. Only the server-side session is gone.
        (await _client.GetAsync("/api/profile", auth.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refresh = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_EndsOnlyTheSessionItWasCalledFrom()
    {
        var (first, userName) = await _client.RegisterNewCustomerAsync();
        var second = await _client.LoginAsync(userName, ApiClientExtensions.StrongPassword);

        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout").WithBearer(first.AccessToken));

        (await _client.GetAsync("/api/profile", second.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndTheOldPairStopsWorking()
    {
        var (original, _) = await _client.RegisterNewCustomerAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest { RefreshToken = original.RefreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        rotated.RefreshToken.Should().NotBe(original.RefreshToken);
        rotated.AccessToken.Should().NotBe(original.AccessToken);

        (await _client.GetAsync("/api/profile", rotated.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/profile", original.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyRotatedToken_IsRefusedAndRevokesEverySessionOfThatUser()
    {
        var (original, _) = await _client.RegisterNewCustomerAsync();

        var rotated = (await (await _client.PostAsJsonAsync(
                "/api/auth/refresh", new RefreshRequest { RefreshToken = original.RefreshToken }))
            .Content.ReadFromJsonAsync<AuthResponse>())!;

        // Someone presents the old token again: it was copied, and nobody can tell by whom.
        var replay = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest { RefreshToken = original.RefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _client.GetAsync("/api/profile", rotated.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PostAsJsonAsync(
                "/api/auth/refresh", new RefreshRequest { RefreshToken = rotated.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest { RefreshToken = "izmisljen-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessToken_WithATamperedPayloadOrSignature_IsRejected()
    {
        var auth = await _client.LoginAsync("customer");
        var parts = auth.AccessToken.Split('.');

        // Same header and signature, but the payload now claims to be someone else.
        var forgedPayload = $"{parts[0]}.{Flip(parts[1])}.{parts[2]}";
        var forgedSignature = $"{parts[0]}.{parts[1]}.{Flip(parts[2])}";
        var unsigned = $"{parts[0]}.{parts[1]}.";

        foreach (var forged in new[] { forgedPayload, forgedSignature, unsigned })
        {
            (await _client.GetAsync("/api/profile", forged))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task ChangePassword_RequiresTheCurrentPassword_AndSignsOutOtherDevices()
    {
        const string newPassword = "NovaLozinka!456";

        var (thisDevice, userName) = await _client.RegisterNewCustomerAsync();
        var otherDevice = await _client.LoginAsync(userName, ApiClientExtensions.StrongPassword);

        var wrong = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = "Pogresna!123",
                NewPassword = newPassword,
                ConfirmNewPassword = newPassword,
            },
            thisDevice.AccessToken);

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadFromJsonAsync<ApiErrorResponse>())!
            .Errors.Should().ContainKey("currentPassword");

        var changed = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = ApiClientExtensions.StrongPassword,
                NewPassword = newPassword,
                ConfirmNewPassword = newPassword,
            },
            thisDevice.AccessToken);

        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.GetAsync("/api/profile", thisDevice.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/profile", otherDevice.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _client.LoginAsync(userName, newPassword)).AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    // The first character, not the last: the final base64url character of a 32-byte signature
    // carries padding bits, so changing it can leave the decoded bytes - and the token - intact.
    private static string Flip(string segment)
    {
        var first = segment[0] == 'A' ? 'B' : 'A';
        return first + segment[1..];
    }
}
