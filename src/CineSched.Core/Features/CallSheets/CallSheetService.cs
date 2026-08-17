using CineSched.Core.Common;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Projects;

namespace CineSched.Core.Features.CallSheets;

public sealed class CallSheetService(ProjectService projects)
{
    public Result<CallSheetData> Initialize(Guid dayId)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null)
        {
            return Result<CallSheetData>.Failure("schedule.invalid-target", "The shoot day does not exist.");
        }

        var production = document.ProductionInfo ?? new ProductionInfo();
        day.CallSheet ??= new CallSheetData();

        var characters = day.Scenes
            .Where(scene => !scene.IsBanner && !scene.IsCalendarEvent)
            .SelectMany(scene => scene.Cast.Select(character => new { Character = character.Trim(), scene.SceneNumber }))
            .Where(value => value.Character.Length > 0)
            .GroupBy(value => value.Character, StringComparer.OrdinalIgnoreCase);

        foreach (var character in characters)
        {
            if (day.CallSheet.CastCallEntries.Any(entry =>
                    string.Equals(entry.CharacterName, character.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var roster = production.CastList.FirstOrDefault(member =>
                string.Equals(member.CharacterName.Trim(), character.Key, StringComparison.OrdinalIgnoreCase));
            day.CallSheet.CastCallEntries.Add(new CastCallEntry
            {
                CharacterName = roster?.CharacterName ?? character.Key,
                ActorName = roster?.ActorName ?? string.Empty,
                SceneNumbers = string.Join(", ", character.Select(value => value.SceneNumber)
                    .Where(number => !string.IsNullOrWhiteSpace(number)).Distinct(StringComparer.OrdinalIgnoreCase))
            });
        }

        var knownLocations = production.LocationRoster.Concat(day.CallSheet.Locations).ToList();
        foreach (var scene in day.Scenes.Where(scene => !string.IsNullOrWhiteSpace(scene.RealLocation)))
        {
            var existing = knownLocations.FirstOrDefault(location =>
                string.Equals(location.Name.Trim(), scene.RealLocation.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(location.Address.Trim(), scene.LocationAddress.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new Location { Name = scene.RealLocation.Trim(), Address = scene.LocationAddress.Trim() };
                knownLocations.Add(existing);
            }

            if (!day.CallSheet.Locations.Any(location => location.Id == existing.Id))
            {
                day.CallSheet.Locations.Add(existing);
            }
        }

        var dailyIds = production.Crew.Where(member => member.IsDailyDefault).Select(member => member.Id).ToList();
        day.CallSheet.CrewIDOverride ??= dailyIds;
        projects.Apply(document, "callsheet.initialized", structural: false);
        return Result<CallSheetData>.Success(day.CallSheet);
    }

    public Result<CallSheetData> Update(Guid dayId, Action<CallSheetData> edit)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null)
        {
            return Result<CallSheetData>.Failure("schedule.invalid-target", "The shoot day does not exist.");
        }

        edit(day.CallSheet);
        projects.Apply(document, "callsheet.updated", structural: false);
        return Result<CallSheetData>.Success(day.CallSheet);
    }

    public IReadOnlyList<CrewMember> GetSelectedCrew(Guid dayId)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null)
        {
            return [];
        }

        var selected = day.CallSheet.CrewIDOverride?.ToHashSet() ?? [];
        return (document.ProductionInfo?.Crew ?? [])
            .Where(member => selected.Contains(member.Id))
            .ToList();
    }

    public IReadOnlyList<CastCallEntry> GetResolvedCastEntries(Guid dayId)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null) return [];
        var roster = document.ProductionInfo?.CastList ?? [];
        return day.CallSheet.CastCallEntries.Select(entry =>
        {
            var member = roster.FirstOrDefault(candidate => string.Equals(
                candidate.CharacterName.Trim(), entry.CharacterName.Trim(), StringComparison.OrdinalIgnoreCase));
            return new CastCallEntry
            {
                Id = entry.Id,
                CharacterName = member?.CharacterName ?? entry.CharacterName,
                ActorName = member?.ActorName ?? entry.ActorName,
                SceneNumbers = entry.SceneNumbers,
                Ecdt = entry.Ecdt,
                PickupTime = entry.PickupTime,
                HmuWardrobeTime = entry.HmuWardrobeTime,
                OnSetTime = entry.OnSetTime,
                WrapTime = entry.WrapTime,
                LocationIndex = entry.LocationIndex
            };
        }).ToList();
    }

    public IReadOnlyList<Location> GetResolvedLocations(Guid dayId)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null) return [];
        var roster = document.ProductionInfo?.LocationRoster ?? [];
        return day.CallSheet.Locations.Select(location =>
        {
            var current = roster.FirstOrDefault(candidate => candidate.Id == location.Id);
            return current ?? location;
        }).ToList();
    }

    public bool HasData(Guid dayId) =>
        projects.Snapshot().ShootDays.FirstOrDefault(day => day.Id == dayId)?.CallSheet.HasData == true;
}
