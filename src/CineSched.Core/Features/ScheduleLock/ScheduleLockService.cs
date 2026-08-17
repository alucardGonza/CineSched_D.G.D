using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.ScheduleLock;

public sealed class ScheduleLockService(ProjectService projects)
{
    public ScheduleLockData Lock(DateTimeOffset now)
    {
        var document = projects.Snapshot();
        document.ProductionInfo ??= new();
        var scheduleLock = new ScheduleLockData
        {
            LockedAt = now,
            WorkingDays = GetWorkingDays(document)
        };
        document.ProductionInfo.ScheduleLock = scheduleLock;
        projects.Apply(document, "schedule.locked");
        return scheduleLock;
    }

    public void Unlock()
    {
        var document = projects.Snapshot();
        if (document.ProductionInfo is null || document.ProductionInfo.ScheduleLock is null)
        {
            return;
        }

        document.ProductionInfo.ScheduleLock = null;
        projects.Apply(document, "schedule.unlocked");
    }

    public IReadOnlyList<ScheduleLockChange> GetChanges()
    {
        var document = projects.Snapshot();
        var baseline = document.ProductionInfo?.ScheduleLock;
        if (baseline is null)
        {
            return [];
        }

        var current = GetWorkingDays(document);
        var keys = baseline.WorkingDays.Keys.Union(current.Keys, StringComparer.OrdinalIgnoreCase);
        var changes = new List<ScheduleLockChange>();
        foreach (var key in keys)
        {
            var oldDays = (baseline.WorkingDays.GetValueOrDefault(key) ?? []).Select(DateOnlyValue).ToHashSet();
            var newDays = (current.GetValueOrDefault(key) ?? []).Select(DateOnlyValue).ToHashSet();
            var added = newDays.Except(oldDays).Order().Select(day => ToOffset(day, document)).ToList();
            var removed = oldDays.Except(newDays).Order().Select(day => ToOffset(day, document)).ToList();
            if (added.Count > 0 || removed.Count > 0)
            {
                changes.Add(new ScheduleLockChange(key, added, removed));
            }
        }

        return changes;
    }

    private static Dictionary<string, List<DateTimeOffset>> GetWorkingDays(ProjectDocument document)
    {
        var result = new Dictionary<string, List<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);
        foreach (var day in document.ShootDays.OrderBy(day => day.Date))
        {
            foreach (var character in day.Scenes.Where(IsNormalScene).SelectMany(scene => scene.Cast)
                         .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!result.TryGetValue(character, out var dates))
                {
                    dates = [];
                    result[character] = dates;
                }

                if (!dates.Any(value => DateOnlyValue(value) == DateOnlyValue(day.Date)))
                {
                    dates.Add(day.Date);
                }
            }
        }

        return result;
    }

    private static DateOnly DateOnlyValue(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);

    private static DateTimeOffset ToOffset(DateOnly date, ProjectDocument document)
    {
        var match = document.ShootDays.FirstOrDefault(day => DateOnlyValue(day.Date) == date);
        var offset = match?.Date.Offset ?? TimeSpan.Zero;
        return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, offset);
    }

    private static bool IsNormalScene(Scene scene) => !scene.IsBanner && !scene.IsCalendarEvent;
}
