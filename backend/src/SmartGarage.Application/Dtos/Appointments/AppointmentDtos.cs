using SmartGarage.Application.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Dtos.Appointments;

public class AppointmentListItemResponse
{
    public int Id { get; init; }

    /// <summary>The identifier people use. The database id is never shown to anyone.</summary>
    public string AppointmentNumber { get; init; } = string.Empty;

    public DateTime ScheduledStartUtc { get; init; }

    public DateTime ScheduledEndUtc { get; init; }

    public AppointmentStatus Status { get; init; }

    public AppointmentPaymentStatus PaymentStatus { get; init; }

    public decimal TotalPrice { get; init; }

    public int VehicleId { get; init; }

    public string VehicleDisplay { get; init; } = string.Empty;

    public string CustomerFullName { get; init; } = string.Empty;

    public string? AssignedEmployeeName { get; init; }

    public IReadOnlyList<string> ServiceNames { get; init; } = [];
}

public sealed class AppointmentItemResponse
{
    public int ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int DurationMinutes { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class AppointmentHistoryResponse
{
    public AppointmentStatus? FromStatus { get; init; }

    public AppointmentStatus ToStatus { get; init; }

    public DateTime ChangedAtUtc { get; init; }

    public string ChangedByName { get; init; } = string.Empty;

    public string ActorRole { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

/// <summary>
/// Everything about one appointment, including what the caller may do with it next. The
/// clients draw their buttons from <see cref="AllowedActions"/> and the blocked reasons, so
/// no business rule is re-implemented on a screen.
/// </summary>
public sealed class AppointmentDetailResponse : AppointmentListItemResponse
{
    public string? CustomerNotes { get; set; }

    /// <summary>Staff only. Always null for a customer.</summary>
    public string? InternalNotes { get; set; }

    public int? MileageAtAppointmentKm { get; init; }

    public string? CancellationReason { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public string? AssignedEmployeePhone { get; init; }

    public IReadOnlyList<AppointmentItemResponse> Items { get; init; } = [];

    public IReadOnlyList<AppointmentHistoryResponse> History { get; init; } = [];

    public bool HasReview { get; init; }

    public IReadOnlyList<string> AllowedActions { get; set; } = [];

    /// <summary>Why cancelling or rescheduling is not offered, or null when it is.</summary>
    public string? ChangeBlockedReason { get; set; }

    public bool CanBePaid { get; set; }

    /// <summary>Why the pay button is disabled, or null when payment is possible.</summary>
    public string? PaymentBlockedReason { get; set; }
}

public static class AppointmentActions
{
    public const string Confirm = nameof(Confirm);
    public const string Start = nameof(Start);
    public const string Complete = nameof(Complete);
    public const string Cancel = nameof(Cancel);
    public const string Reschedule = nameof(Reschedule);
    public const string Assign = nameof(Assign);
    public const string LeaveReview = nameof(LeaveReview);
}

/// <summary>
/// A booking. It names what and when, and nothing else: the customer is the signed-in user
/// and the price is computed on the server from the catalogue.
/// </summary>
public sealed class AppointmentCreateRequest
{
    public int VehicleId { get; init; }

    public IReadOnlyList<int> ServiceIds { get; init; } = [];

    public DateTime ScheduledStartUtc { get; init; }

    public string? CustomerNotes { get; init; }
}

public sealed class AppointmentCancelRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class AppointmentCompleteRequest
{
    public int MileageAtAppointmentKm { get; init; }

    public string? InternalNotes { get; init; }
}

public sealed class AppointmentRescheduleRequest
{
    public DateTime NewStartUtc { get; init; }

    public string? Reason { get; init; }
}

public sealed class AppointmentAssignRequest
{
    public int EmployeeId { get; init; }
}

public sealed class AppointmentSearchRequest : BaseSearchRequest
{
    public AppointmentStatus? Status { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public int? VehicleId { get; init; }

    public int? EmployeeId { get; init; }

    /// <summary>Honoured for staff only. A customer always gets their own appointments.</summary>
    public int? CustomerId { get; init; }
}

/// <summary>The counters at the top of the appointment screens.</summary>
public sealed class AppointmentSummaryResponse
{
    public int Total { get; init; }

    public int Pending { get; init; }

    public int Confirmed { get; init; }

    public int InProgress { get; init; }

    public int Completed { get; init; }

    public int Cancelled { get; init; }

    public int Active => Pending + Confirmed + InProgress;
}

public sealed class SlotResponse
{
    public DateTime StartUtc { get; init; }

    public DateTime EndUtc { get; init; }

    public bool IsAvailable { get; init; }

    public int FreePlaces { get; init; }

    /// <summary>Why the slot cannot be booked, or null when it can.</summary>
    public string? UnavailableReason { get; init; }
}

public sealed class SlotSearchRequest
{
    public DateOnly Date { get; init; }

    // A concrete List: the query-string binder fills a list it can add to, and silently
    // leaves a read-only collection empty.
#pragma warning disable CA1002
    public List<int> ServiceIds { get; init; } = [];
#pragma warning restore CA1002
}
