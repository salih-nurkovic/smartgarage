using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Application.Validators;
using SmartGarage.Domain.Constants;
using Xunit;

namespace SmartGarage.UnitTests.Validators;

/// <summary>
/// The instructions spell out how password validation must behave on each kind of form:
/// strict on registration, optional when an administrator edits a user, and with a message
/// that names the rule that was broken. These tests hold the validators to that.
/// </summary>
public sealed class UserValidatorTests
{
    private const string StrongPassword = "Lozinka!123";

    private static readonly IOptions<IdentityOptions> Identity = Options.Create(new IdentityOptions
    {
        Password =
        {
            RequiredLength = 8,
            RequireDigit = true,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
        },
    });

    [Theory]
    [InlineData("Ab!1", "najmanje 8 znakova")]
    [InlineData("Lozinka!abc", "cifru")]
    [InlineData("lozinka!123", "veliko slovo")]
    [InlineData("LOZINKA!123", "malo slovo")]
    [InlineData("Lozinka1234", "specijalni znak")]
    public void Register_NamesTheExactPasswordRuleThatWasBroken(string password, string expectedFragment)
    {
        var result = new RegisterRequestValidator(Identity).Validate(ValidRegistration(password));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterRequest.Password)
            && e.ErrorMessage.Contains(expectedFragment));
    }

    [Fact]
    public void Register_AcceptsAPasswordThatMeetsEveryRule() =>
        new RegisterRequestValidator(Identity)
            .Validate(ValidRegistration(StrongPassword))
            .IsValid.Should().BeTrue();

    [Theory]
    [InlineData("ab")]
    [InlineData("ime sa razmakom")]
    [InlineData("ime@domena")]
    public void Register_RejectsUserNamesOutsideTheAllowedFormat(string userName)
    {
        var request = new RegisterRequest
        {
            UserName = userName,
            Email = "korisnik@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 123 456",
            Password = StrongPassword,
            ConfirmPassword = StrongPassword,
        };

        new RegisterRequestValidator(Identity).Validate(request).Errors
            .Should().Contain(e => e.PropertyName == nameof(RegisterRequest.UserName));
    }

    [Fact]
    public void AdminEdit_WithoutAPassword_IsValid()
    {
        var result = new UserUpdateRequestValidator(Identity).Validate(ValidUpdate(newPassword: null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AdminEdit_WithAPassword_AppliesThePolicyAndRequiresTheConfirmationToMatch()
    {
        var validator = new UserUpdateRequestValidator(Identity);

        validator.Validate(ValidUpdate("slaba", "slaba")).Errors
            .Should().Contain(e => e.PropertyName == nameof(UserUpdateRequest.NewPassword));

        validator.Validate(ValidUpdate(StrongPassword, "Drugacija!123")).Errors
            .Should().Contain(e => e.PropertyName == nameof(UserUpdateRequest.ConfirmNewPassword));

        validator.Validate(ValidUpdate(StrongPassword, StrongPassword)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AdminEdit_RejectsARoleThatDoesNotExist()
    {
        var request = new UserUpdateRequest
        {
            Email = "korisnik@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 123 456",
            Role = "SuperAdmin",
        };

        new UserUpdateRequestValidator(Identity).Validate(request).Errors
            .Should().Contain(e => e.PropertyName == nameof(UserUpdateRequest.Role));
    }

    [Fact]
    public void Deactivation_RequiresAReason_ButActivationDoesNot()
    {
        var validator = new UserStatusRequestValidator();

        validator.Validate(new UserStatusRequest { IsActive = false }).IsValid.Should().BeFalse();
        validator.Validate(new UserStatusRequest { IsActive = false, Reason = "abc" }).IsValid.Should().BeFalse();
        validator.Validate(new UserStatusRequest { IsActive = false, Reason = "Zloupotreba naloga." })
            .IsValid.Should().BeTrue();
        validator.Validate(new UserStatusRequest { IsActive = true }).IsValid.Should().BeTrue();
    }

    private static RegisterRequest ValidRegistration(string password) => new()
    {
        UserName = "novi.korisnik",
        Email = "korisnik@example.com",
        FirstName = "Test",
        LastName = "Korisnik",
        PhoneNumber = "+387 61 123 456",
        Password = password,
        ConfirmPassword = password,
    };

    private static UserUpdateRequest ValidUpdate(string? newPassword, string? confirmation = null) => new()
    {
        Email = "korisnik@example.com",
        FirstName = "Test",
        LastName = "Korisnik",
        PhoneNumber = "+387 61 123 456",
        Role = Roles.Customer,
        NewPassword = newPassword,
        ConfirmNewPassword = confirmation,
    };
}
