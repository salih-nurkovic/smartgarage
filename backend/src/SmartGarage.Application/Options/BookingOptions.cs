namespace SmartGarage.Application.Options;

/// <summary>The booking rules, from <c>.env</c>, read once at startup.</summary>
public sealed class BookingOptions
{
    /// <summary>How many vehicles the garage works on at the same time.</summary>
    public int MaxConcurrent { get; set; }

    /// <summary>Length of one bookable slot. Appointments start on slot boundaries.</summary>
    public int SlotMinutes { get; set; }

    /// <summary>Gap a mechanic needs between two jobs.</summary>
    public int BufferMinutes { get; set; }

    /// <summary>How close to the start a customer may still cancel or reschedule.</summary>
    public int CancellationCutoffHours { get; set; }

    public int MaxDaysAhead { get; set; }

    /// <summary>How long before a confirmed appointment the customer is reminded of it.</summary>
    public int ReminderHoursBefore { get; set; }
}
