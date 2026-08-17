using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.Conflicts;

public sealed class ConflictService(ProjectService projects)
{
    public IReadOnlyList<ScheduleConflict> ScanAvailability()
    {
        var document = projects.Snapshot();
        var roster = document.ProductionInfo?.CastList ?? [];
        var result = new List<ScheduleConflict>();

        foreach (var day in document.ShootDays)
        {
            foreach (var scene in day.Scenes.Where(IsNormalScene))
            {
                foreach (var character in scene.Cast)
                {
                    var member = roster.FirstOrDefault(candidate =>
                        string.Equals(candidate.CharacterName.Trim(), character.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (member is null || !member.UnavailableRanges.Any(range => range.Contains(day.Date)))
                    {
                        continue;
                    }

                    result.Add(new ScheduleConflict(
                        scene.Id,
                        day.Id,
                        day.Date,
                        member.CharacterName,
                        member.ActorName,
                        day.IsBlackout));
                }
            }
        }

        return result;
    }

    public IReadOnlyList<ScheduleConflict> ScanBlackouts()
    {
        var document = projects.Snapshot();
        return document.ShootDays
            .Where(day => day.IsBlackout)
            .SelectMany(day => day.Scenes.Where(IsNormalScene).Select(scene =>
                new ScheduleConflict(scene.Id, day.Id, day.Date, string.Empty, string.Empty, true)))
            .ToList();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> ScanDuplicateSceneNumbers()
    {
        var all = GetAllUniqueScenes(projects.Snapshot());
        return all
            .Where(scene => IsNormalScene(scene) && !string.IsNullOrWhiteSpace(scene.SceneNumber))
            .GroupBy(scene => scene.SceneNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(scene => scene.Id).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Scene> GetAllUniqueScenes(ProjectDocument document) =>
        document.AllScenes.Concat(document.ShootDays.SelectMany(day => day.Scenes))
            .GroupBy(scene => scene.Id)
            .Select(group => group.First());

    private static bool IsNormalScene(Scene scene) => !scene.IsBanner && !scene.IsCalendarEvent;
}
