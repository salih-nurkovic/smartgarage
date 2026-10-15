using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.StateMachine;

/// <summary>What one allowed status change demands of whoever makes it.</summary>
public sealed record TransitionRule(IReadOnlySet<string> AllowedRoles, bool RequiresReason);

/// <summary>
/// The appointment state machine: every status change that is allowed, who may make it, and
/// whether it needs a reason.
/// <para>
/// This table is the single definition. The instructions require the logic to be centralised
/// rather than spread through controllers, so nothing else in the code decides whether a
/// status may follow another - the service asks here, and the clients are told the answer.
/// A pair that is not in the table is not a transition.
/// </para>
/// </summary>
public static class AppointmentStateMachine
{
    private static readonly IReadOnlySet<string> Staff =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Administrator, Roles.Employee };

    private static readonly IReadOnlySet<string> Anyone =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Administrator, Roles.Employee, Roles.Customer };

    // Besides people, the scheduled job may cancel a booking nobody confirmed in time.
    private static readonly IReadOnlySet<string> AnyoneOrSystem =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Roles.Administrator, Roles.Employee, Roles.Customer, Roles.System,
        };

    private static readonly IReadOnlySet<string> AdministratorOnly =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Administrator };

    private static readonly Dictionary<(AppointmentStatus From, AppointmentStatus To), TransitionRule> Rules = new()
    {
        [(AppointmentStatus.Pending, AppointmentStatus.Confirmed)] = new(Staff, RequiresReason: false),
        [(AppointmentStatus.Pending, AppointmentStatus.Cancelled)] = new(AnyoneOrSystem, RequiresReason: true),

        [(AppointmentStatus.Confirmed, AppointmentStatus.InProgress)] = new(Staff, RequiresReason: false),
        [(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)] = new(Anyone, RequiresReason: true),

        [(AppointmentStatus.InProgress, AppointmentStatus.Completed)] = new(Staff, RequiresReason: false),

        // Work has already begun on the vehicle, so stopping it is an administrator's call.
        [(AppointmentStatus.InProgress, AppointmentStatus.Cancelled)] = new(AdministratorOnly, RequiresReason: true),
    };

    /// <summary>Every transition in the table, for tests and documentation.</summary>
    public static IReadOnlyCollection<(AppointmentStatus From, AppointmentStatus To)> AllTransitions => Rules.Keys;

    public static TransitionRule? FindRule(AppointmentStatus from, AppointmentStatus to) =>
        Rules.GetValueOrDefault((from, to));

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to, IEnumerable<string> roles) =>
        FindRule(from, to) is { } rule && roles.Any(rule.AllowedRoles.Contains);

    /// <summary>The statuses this actor may move the appointment to from where it is now.</summary>
    public static IReadOnlyList<AppointmentStatus> NextStatuses(AppointmentStatus from, IReadOnlyCollection<string> roles) =>
        Rules
            .Where(rule => rule.Key.From == from && roles.Any(rule.Value.AllowedRoles.Contains))
            .Select(rule => rule.Key.To)
            .ToList();

    /// <summary>Completed and cancelled appointments never change status again.</summary>
    public static bool IsTerminal(AppointmentStatus status) =>
        status is AppointmentStatus.Completed or AppointmentStatus.Cancelled;

    /// <summary>The statuses that hold a place in the schedule.</summary>
    public static bool IsActive(AppointmentStatus status) => !IsTerminal(status);
}
