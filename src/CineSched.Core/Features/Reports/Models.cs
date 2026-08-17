using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scheduling;

namespace CineSched.Core.Features.Reports;

public enum ReportKind
{
    Schedule,
    Stripboard,
    ShootingSchedule,
    DaysOutOfDays,
    Breakdown,
    CallSheet
}

public enum ReportLanguage
{
    English,
    Spanish
}

public sealed record ReportRequest(
    ProjectDocument Project,
    ReportLanguage Language,
    bool IncludeHoldDays = true,
    ShootDay? ShootDay = null);
