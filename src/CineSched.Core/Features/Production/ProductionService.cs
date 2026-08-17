using CineSched.Core.Common;
using CineSched.Core.Features.Projects;

namespace CineSched.Core.Features.Production;

public sealed class ProductionService(ProjectService projects)
{
    public ProductionInfo Get() => projects.Snapshot().ProductionInfo ?? new ProductionInfo();

    public ProductionInfo Update(Action<ProductionInfo> edit)
    {
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        edit(document.ProductionInfo);
        document.ProductionInfo.LocationRoster = NormalizeLocations(document.ProductionInfo.LocationRoster);
        projects.Apply(document, "production.updated", structural: false);
        return projects.Snapshot().ProductionInfo!;
    }

    public Result<CastMember> UpdateCast(Guid id, string actorName, string characterName)
    {
        var document = projects.Snapshot();
        var member = document.ProductionInfo?.CastList.FirstOrDefault(candidate => candidate.Id == id);
        if (member is null)
        {
            return Result<CastMember>.Failure("production.cast-not-found", "The cast member does not exist.");
        }

        member.ActorName = actorName.Trim();
        member.CharacterName = characterName.Trim();
        projects.Apply(document, "production.cast-updated", structural: false);
        return Result<CastMember>.Success(member);
    }

    public Result<CrewMember> UpdateCrew(Guid id, string name, string role, string phone, bool isDailyDefault)
    {
        var document = projects.Snapshot();
        var member = document.ProductionInfo?.Crew.FirstOrDefault(candidate => candidate.Id == id);
        if (member is null)
        {
            return Result<CrewMember>.Failure("production.crew-not-found", "The crew member does not exist.");
        }

        member.Name = name.Trim();
        member.Role = role.Trim();
        member.Phone = phone.Trim();
        member.IsDailyDefault = isDailyDefault;
        projects.Apply(document, "production.crew-updated", structural: false);
        return Result<CrewMember>.Success(member);
    }

    public Result<Location> UpdateLocation(Guid id, string name, string address)
    {
        var document = projects.Snapshot();
        var location = document.ProductionInfo?.LocationRoster.FirstOrDefault(candidate => candidate.Id == id);
        if (location is null)
        {
            return Result<Location>.Failure("production.location-not-found", "The location does not exist.");
        }

        location.Name = name.Trim();
        location.Address = address.Trim();
        projects.Apply(document, "production.location-updated", structural: false);
        return Result<Location>.Success(location);
    }

    public IReadOnlyList<Location> NormalizeRoster()
    {
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        document.ProductionInfo.LocationRoster = NormalizeLocations(document.ProductionInfo.LocationRoster);
        projects.Apply(document, "production.locations-normalized", structural: false);
        return document.ProductionInfo.LocationRoster;
    }

    private static List<Location> NormalizeLocations(IEnumerable<Location> locations) =>
        locations
            .GroupBy(location => $"{location.Name.Trim()}\u001f{location.Address.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
}
