using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// One weekday of an employee's working hours.
/// <para>
/// This is what makes "real-time available appointment slots" computable on the server. The
/// approved registration says slots are offered "uzimajuci u obzir kapacitet servisa i
/// zauzetost mehanicara" - without stored working hours that sentence has no implementation.
/// </para>
/// </summary>
public class EmployeeSchedule : BaseEntity
{
    public int EmployeeProfileId { get; set; }

    /// <summary>0 = Sunday through 6 = Saturday, matching <see cref="System.DayOfWeek"/>.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsWorkingDay { get; set; } = true;

    public EmployeeProfile EmployeeProfile { get; set; } = null!;
}
