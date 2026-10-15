using FluentAssertions;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.StateMachine;
using Xunit;

namespace SmartGarage.UnitTests.Appointments;

/// <summary>
/// Checks the transition table against every combination of status, status and role - the
/// allowed ones and, more importantly, all the ones that must be refused.
/// </summary>
public sealed class AppointmentStateMachineTests
{
    private static readonly AppointmentStatus[] Statuses = Enum.GetValues<AppointmentStatus>();

    // The whole workflow, written out. Anything not listed here must not be possible.
    private static readonly HashSet<(AppointmentStatus From, AppointmentStatus To, string Role)> Allowed =
    [
        (AppointmentStatus.Pending, AppointmentStatus.Confirmed, Roles.Administrator),
        (AppointmentStatus.Pending, AppointmentStatus.Confirmed, Roles.Employee),
        (AppointmentStatus.Pending, AppointmentStatus.Cancelled, Roles.Administrator),
        (AppointmentStatus.Pending, AppointmentStatus.Cancelled, Roles.Employee),
        (AppointmentStatus.Pending, AppointmentStatus.Cancelled, Roles.Customer),
        (AppointmentStatus.Confirmed, AppointmentStatus.InProgress, Roles.Administrator),
        (AppointmentStatus.Confirmed, AppointmentStatus.InProgress, Roles.Employee),
        (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, Roles.Administrator),
        (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, Roles.Employee),
        (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, Roles.Customer),
        (AppointmentStatus.InProgress, AppointmentStatus.Completed, Roles.Administrator),
        (AppointmentStatus.InProgress, AppointmentStatus.Completed, Roles.Employee),
        (AppointmentStatus.InProgress, AppointmentStatus.Cancelled, Roles.Administrator),
    ];

    public static TheoryData<AppointmentStatus, AppointmentStatus, string> EveryCombination()
    {
        var data = new TheoryData<AppointmentStatus, AppointmentStatus, string>();

        foreach (var from in Statuses)
        {
            foreach (var to in Statuses)
            {
                foreach (var role in Roles.All)
                {
                    data.Add(from, to, role);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public void CanTransition_AllowsExactlyTheDocumentedWorkflow(
        AppointmentStatus from,
        AppointmentStatus to,
        string role) =>
        AppointmentStateMachine.CanTransition(from, to, [role])
            .Should().Be(Allowed.Contains((from, to, role)));

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void TerminalStatuses_LeadNowhere_ForAnyone(AppointmentStatus terminal)
    {
        AppointmentStateMachine.IsTerminal(terminal).Should().BeTrue();
        AppointmentStateMachine.NextStatuses(terminal, [.. Roles.All]).Should().BeEmpty();
    }

    [Fact]
    public void EveryCancellation_RequiresAReason_AndNothingElseDoes()
    {
        foreach (var (from, to) in AppointmentStateMachine.AllTransitions)
        {
            AppointmentStateMachine.FindRule(from, to)!.RequiresReason
                .Should().Be(to == AppointmentStatus.Cancelled, $"{from} -> {to}");
        }
    }

    [Fact]
    public void NoStatusCanTransitionToItself_OrBackwards()
    {
        foreach (var (from, to) in AppointmentStateMachine.AllTransitions)
        {
            ((int)to).Should().BeGreaterThan((int)from, $"{from} -> {to} must move forward");
        }
    }

    [Fact]
    public void TheSystem_MayOnlyCancelABookingThatWasNeverConfirmed()
    {
        foreach (var from in Statuses)
        {
            foreach (var to in Statuses)
            {
                AppointmentStateMachine.CanTransition(from, to, [Roles.System]).Should().Be(
                    from == AppointmentStatus.Pending && to == AppointmentStatus.Cancelled,
                    $"{from} -> {to}");
            }
        }

        Roles.All.Should().NotContain(Roles.System, "it must never be assignable to an account");
    }

    [Fact]
    public void NextStatuses_OfferACustomerOnlyCancellation()
    {
        AppointmentStateMachine.NextStatuses(AppointmentStatus.Pending, [Roles.Customer])
            .Should().Equal(AppointmentStatus.Cancelled);
        AppointmentStateMachine.NextStatuses(AppointmentStatus.InProgress, [Roles.Customer])
            .Should().BeEmpty();
        AppointmentStateMachine.NextStatuses(AppointmentStatus.Pending, [Roles.Administrator])
            .Should().BeEquivalentTo([AppointmentStatus.Confirmed, AppointmentStatus.Cancelled]);
    }
}
