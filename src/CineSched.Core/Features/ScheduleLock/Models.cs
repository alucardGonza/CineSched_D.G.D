namespace CineSched.Core.Features.ScheduleLock;

public sealed class ScheduleLockData
{
    public DateTimeOffset LockedAt { get; set; }
    public Dictionary<string, List<DateTimeOffset>> WorkingDays { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ScheduleLockChange(
    string ActorDisplayName,
    IReadOnlyList<DateTimeOffset> AddedDays,
    IReadOnlyList<DateTimeOffset> RemovedDays)
{
    public string DisplayString => string.Join(" · ", new[]
    {
        AddedDays.Count == 0 ? null : $"+ {string.Join(", ", AddedDays.Select(day => day.ToString("yyyy-MM-dd")))}",
        RemovedDays.Count == 0 ? null : $"− {string.Join(", ", RemovedDays.Select(day => day.ToString("yyyy-MM-dd")))}"
    }.Where(value => value is not null));
}
