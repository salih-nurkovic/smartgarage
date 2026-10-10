using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Constants;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Users;

[Collection(ApiTestGroup.Name)]
public sealed class UserAdministrationTests
{
    private readonly HttpClient _client;

    public UserAdministrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("employee")]
    public async Task UserManagement_ForAnyoneButAnAdministrator_Returns403(string userName)
    {
        var auth = await _client.LoginAsync(userName);

        (await _client.GetAsync("/api/users", auth.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deactivate = await _client.SendJsonAsync(
            HttpMethod.Patch,
            $"/api/users/{auth.User.Id}/status",
            new UserStatusRequest { IsActive = false, Reason = "Pokušaj bez dozvole." },
            auth.AccessToken);

        deactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_IsPagedSearchableAndNeverExceedsTheMaximumPageSize()
    {
        var admin = await _client.LoginAsync("admin");

        var huge = await GetPageAsync("/api/users?pageSize=100000", admin.AccessToken);
        huge.PageSize.Should().Be(PagingLimits.MaxPageSize);
        huge.TotalCount.Should().BeGreaterThanOrEqualTo(12);

        var small = await GetPageAsync("/api/users?pageSize=5&page=2", admin.AccessToken);
        small.Items.Should().HaveCount(5);
        small.Page.Should().Be(2);
        small.HasPrevious.Should().BeTrue();

        var search = await GetPageAsync("/api/users?search=mehanicar", admin.AccessToken);
        search.Items.Should().OnlyContain(u => u.UserName.Contains("mehanicar"));
        search.Items.Should().HaveCount(2);

        var employees = await GetPageAsync($"/api/users?role={Roles.Employee}", admin.AccessToken);
        employees.Items.Should().OnlyContain(u => u.Roles.Contains(Roles.Employee));
        employees.Items.Should().HaveCount(3);

        var inactive = await GetPageAsync("/api/users?isActive=false", admin.AccessToken);
        inactive.Items.Should().Contain(u => u.UserName == "neaktivan");
        inactive.Items.Should().OnlyContain(u => !u.IsActive);
    }

    [Fact]
    public async Task Deactivation_TakesEffectAtOnce_AndReactivationRestoresAccess()
    {
        var admin = await _client.LoginAsync("admin");
        var (victim, userName) = await _client.RegisterNewCustomerAsync();

        var withoutReason = await _client.SendJsonAsync(
            HttpMethod.Patch,
            $"/api/users/{victim.User.Id}/status",
            new UserStatusRequest { IsActive = false },
            admin.AccessToken);

        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withoutReason.Content.ReadFromJsonAsync<ApiErrorResponse>())!
            .Errors.Should().ContainKey("reason");

        var deactivated = await _client.SendJsonAsync(
            HttpMethod.Patch,
            $"/api/users/{victim.User.Id}/status",
            new UserStatusRequest { IsActive = false, Reason = "Zloupotreba naloga." },
            admin.AccessToken);

        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);

        // The token the user already holds stops working without waiting for it to expire.
        (await _client.GetAsync("/api/profile", victim.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _client.PostAsJsonAsync(
                "/api/auth/refresh", new RefreshRequest { RefreshToken = victim.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest { UserName = userName, Password = ApiClientExtensions.StrongPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reactivated = await _client.SendJsonAsync(
            HttpMethod.Patch,
            $"/api/users/{victim.User.Id}/status",
            new UserStatusRequest { IsActive = true },
            admin.AccessToken);

        reactivated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.LoginAsync(userName, ApiClientExtensions.StrongPassword))
            .AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Administrator_CannotDeactivateOrDemoteThemselves()
    {
        var admin = await _client.LoginAsync("admin");

        var deactivate = await _client.SendJsonAsync(
            HttpMethod.Patch,
            $"/api/users/{admin.User.Id}/status",
            new UserStatusRequest { IsActive = false, Reason = "Greškom kliknuto." },
            admin.AccessToken);

        deactivate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var demote = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/users/{admin.User.Id}",
            UpdateFrom(admin.User, Roles.Customer),
            admin.AccessToken);

        demote.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_WithoutAPassword_LeavesThePasswordAlone()
    {
        var admin = await _client.LoginAsync("admin");
        var (customer, userName) = await _client.RegisterNewCustomerAsync();

        var request = new UserUpdateRequest
        {
            Email = customer.User.Email,
            FirstName = "Izmijenjeno",
            LastName = "Prezime",
            PhoneNumber = "+387 62 111 222",
            Role = Roles.Customer,
        };

        var response = await _client.SendJsonAsync(
            HttpMethod.Put, $"/api/users/{customer.User.Id}", request, admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserResponse>())!.FirstName.Should().Be("Izmijenjeno");

        // Same password and same session as before the edit.
        (await _client.GetAsync("/api/profile", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LoginAsync(userName, ApiClientExtensions.StrongPassword))
            .User.FirstName.Should().Be("Izmijenjeno");
    }

    [Fact]
    public async Task Update_WithANewPassword_NeedsNoOldPassword_AndEndsTheUsersSessions()
    {
        const string newPassword = "AdminPostavio!1";

        var admin = await _client.LoginAsync("admin");
        var (customer, userName) = await _client.RegisterNewCustomerAsync();

        var weak = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/users/{customer.User.Id}",
            new { customer.User.Email, customer.User.FirstName, customer.User.LastName, customer.User.PhoneNumber, role = Roles.Customer, newPassword = "slaba", confirmNewPassword = "slaba" },
            admin.AccessToken);

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.Content.ReadFromJsonAsync<ApiErrorResponse>())!
            .Errors.Should().ContainKey("newPassword");

        var response = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/users/{customer.User.Id}",
            new { customer.User.Email, customer.User.FirstName, customer.User.LastName, customer.User.PhoneNumber, role = Roles.Customer, newPassword, confirmNewPassword = newPassword },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.GetAsync("/api/profile", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.LoginAsync(userName, newPassword)).AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_LetsAnAdministratorChooseTheRole_AndTheNewAccountCanSignIn()
    {
        var admin = await _client.LoginAsync("admin");
        var userName = $"novi{Guid.NewGuid():N}"[..20];

        var response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/users",
            new UserCreateRequest
            {
                UserName = userName,
                Email = $"{userName}@example.com",
                FirstName = "Novi",
                LastName = "Administrator",
                PhoneNumber = "+387 33 555 666",
                Password = ApiClientExtensions.StrongPassword,
                ConfirmPassword = ApiClientExtensions.StrongPassword,
                Role = Roles.Administrator,
            },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await _client.LoginAsync(userName, ApiClientExtensions.StrongPassword);
        created.User.Roles.Should().Equal(Roles.Administrator);

        // Newest first, so the record just added is at the top of the list.
        var firstPage = await GetPageAsync("/api/users?pageSize=1", admin.AccessToken);
        firstPage.Items.Single().UserName.Should().Be(userName);

        (await _client.GetAsync("/api/users/999999", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Profile_IsAlwaysTheCallersOwn_AndRejectsAnEmailAnotherAccountHolds()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();

        var updated = await _client.SendJsonAsync(
            HttpMethod.Put,
            "/api/profile",
            new { id = 1, userId = 1, email = customer.User.Email, firstName = "Moje", lastName = "Ime", phoneNumber = "+387 61 222 333" },
            customer.AccessToken);

        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = (await updated.Content.ReadFromJsonAsync<UserResponse>())!;
        profile.Id.Should().Be(customer.User.Id);
        profile.FirstName.Should().Be("Moje");

        var taken = await _client.SendJsonAsync(
            HttpMethod.Put,
            "/api/profile",
            new ProfileUpdateRequest
            {
                Email = "admin@smartgarage.ba",
                FirstName = "Moje",
                LastName = "Ime",
                PhoneNumber = "+387 61 222 333",
            },
            customer.AccessToken);

        taken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await taken.Content.ReadFromJsonAsync<ApiErrorResponse>())!
            .Errors.Should().ContainKey("email");
    }

    private static UserUpdateRequest UpdateFrom(UserResponse user, string role) => new()
    {
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber,
        Role = role,
    };

    private async Task<PagedResult<UserResponse>> GetPageAsync(string url, string accessToken)
    {
        var response = await _client.GetAsync(url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResult<UserResponse>>())!;
    }
}
