namespace SmartGarage.Contracts.Events;

/// <summary>A customer booked an appointment.</summary>
public sealed record AppointmentCreatedEvent : IntegrationEvent
{
    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int CustomerId { get; init; }

    public required string CustomerFullName { get; init; }

    public required DateTime ScheduledStartUtc { get; init; }
}

/// <summary>An appointment moved from one status to another.</summary>
public sealed record AppointmentStatusChangedEvent : IntegrationEvent
{
    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int CustomerId { get; init; }

    public required string CustomerFullName { get; init; }

    public required string FromStatus { get; init; }

    public required string ToStatus { get; init; }

    public string? Reason { get; init; }

    public required DateTime ScheduledStartUtc { get; init; }

    /// <summary>True when the customer made the change, so staff are the ones to be told.</summary>
    public required bool ChangedByCustomer { get; init; }
}

/// <summary>A confirmed appointment is coming up. Raised by the Worker's scheduled scan.</summary>
public sealed record AppointmentReminderDueEvent : IntegrationEvent
{
    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int CustomerId { get; init; }

    public required DateTime ScheduledStartUtc { get; init; }
}

/// <summary>A vehicle is due for a service, by date or by mileage. Raised by the Worker's scheduled scan.</summary>
public sealed record ServiceReminderDueEvent : IntegrationEvent
{
    public required int ReminderId { get; init; }

    public required int OwnerId { get; init; }

    public required int VehicleId { get; init; }

    public required string VehicleDisplay { get; init; }

    public required string ServiceName { get; init; }
}

/// <summary>An appointment was moved to a different time.</summary>
public sealed record AppointmentRescheduledEvent : IntegrationEvent
{
    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int CustomerId { get; init; }

    public required string CustomerFullName { get; init; }

    public required DateTime OldStartUtc { get; init; }

    public required DateTime NewStartUtc { get; init; }

    public string? Reason { get; init; }

    public required bool ChangedByCustomer { get; init; }
}
