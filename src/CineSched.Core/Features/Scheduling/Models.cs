using CineSched.Core.Features.CallSheets;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.Scheduling;

public sealed class ShootDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Date { get; set; }
    public List<Scene> Scenes { get; set; } = [];
    public CallSheetData CallSheet { get; set; } = new();
    public bool IsBlackout { get; set; }

    public int TotalDuration => Scenes.Where(scene => !scene.IsCalendarEvent && !scene.IsBanner).Sum(scene => scene.Duration);
    public int TotalEstimatedTime => Scenes.Where(scene => !scene.IsCalendarEvent).Sum(scene => scene.EstimatedTime);
}

public sealed record ChangeDateRangeRequest(DateTimeOffset Start, DateTimeOffset End, bool ShiftExisting);
public sealed record MoveScenesRequest(IReadOnlyList<Guid> SceneIds, Guid? SourceDayId, Guid TargetDayId, int TargetIndex);
public sealed record ReorderScenesRequest(Guid DayId, IReadOnlyList<Guid> SceneIds, int TargetIndex);
public sealed record MoveWholeDayRequest(Guid SourceDayId, Guid TargetDayId);
public sealed record SetBlackoutRequest(Guid DayId, bool IsBlackout, bool ApplyToMatchingWeekday = false);
public sealed record CalendarSlot(DateOnly Date, ShootDay? ShootDay, bool IsInRange);
