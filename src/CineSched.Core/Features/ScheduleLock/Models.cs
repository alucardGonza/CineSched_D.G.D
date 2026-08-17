namespace CineSched.Core.Features.ScheduleLock;

public sealed class ScheduleLockData
{
    public DateTimeOffset LockedAt { get; set; }
    public Dictionary<string, List<DateTimeOffset>> WorkingDays { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ScheduleLockChange(
    string ActorDisplayName,
    IReadOnlyList<DateTimeOffset> AddedDays,
    IReadOnlyList<DateTimeOffset> RemovedDays);
