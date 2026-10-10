using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.Employees;

/// <summary>One day of an employee's working week.</summary>
public sealed class EmployeeScheduleDay
{
    public DayOfWeek DayOfWeek { get; init; }

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public bool IsWorkingDay { get; init; }
}

public sealed class EmployeeResponse
{
    public int Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string? Specialization { get; init; }

    public DateTime HireDateUtc { get; init; }

    public bool IsActive { get; init; }

    public bool IsAvailableForAssignment { get; init; }

    public int CompletedJobs { get; init; }

    public double? AverageRating { get; init; }

    public IReadOnlyList<EmployeeScheduleDay> Schedule { get; init; } = [];
}

/// <summary>Creates the login and the employment record together, so neither exists alone.</summary>
public sealed class EmployeeCreateRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string? Specialization { get; init; }

    public DateOnly HireDate { get; init; }
}

public sealed class EmployeeUpdateRequest
{
    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string? Specialization { get; init; }

    public DateOnly HireDate { get; init; }

    public bool IsAvailableForAssignment { get; init; } = true;
}

public sealed class EmployeeScheduleRequest
{
    public IReadOnlyList<EmployeeScheduleDay> Days { get; init; } = [];
}

public sealed class EmployeeSearchRequest : BaseSearchRequest
{
    public bool? IsActive { get; init; }

    public bool? IsAvailableForAssignment { get; init; }
}
