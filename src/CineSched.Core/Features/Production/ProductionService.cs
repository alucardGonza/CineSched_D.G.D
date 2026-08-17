using CineSched.Core.Common;
using CineSched.Core.Features.Projects;

namespace CineSched.Core.Features.Production;

public sealed class ProductionService(ProjectService projects)
{
    public ProductionInfo Get() => projects.Snapshot().ProductionInfo ?? new ProductionInfo();

    public CastMember AddCast(string actorName, string characterName)
    {
        var member = new CastMember { ActorName = actorName.Trim(), CharacterName = characterName.Trim() };
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        document.ProductionInfo.CastList.Add(member);
        projects.Apply(document, "production.cast-added", structural: false);
        return member;
    }

    public CrewMember AddCrew(string name, string role, string phone, bool isDailyDefault)
    {
        var member = new CrewMember
        {
            Name = name.Trim(), Role = role.Trim(), Phone = phone.Trim(), IsDailyDefault = isDailyDefault
        };
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        document.ProductionInfo.Crew.Add(member);
        projects.Apply(document, "production.crew-added", structural: false);
        return member;
    }

    public Location AddLocation(string name, string address)
    {
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        var existing = document.ProductionInfo.LocationRoster.FirstOrDefault(location =>
            string.Equals(location.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(location.Address.Trim(), address.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var location = new Location { Name = name.Trim(), Address = address.Trim() };
        document.ProductionInfo.LocationRoster.Add(location);
        projects.Apply(document, "production.location-added", structural: false);
        return location;
    }

    public Result<Unit> RemoveCast(Guid id) => RemoveFromRoster(
        info => info.CastList.RemoveAll(member => member.Id == id) > 0, "production.cast-not-found", "production.cast-removed");

    public Result<Unit> RemoveCrew(Guid id) => RemoveFromRoster(
        info => info.Crew.RemoveAll(member => member.Id == id) > 0, "production.crew-not-found", "production.crew-removed");

    public Result<Unit> RemoveLocation(Guid id) => RemoveFromRoster(
        info => info.LocationRoster.RemoveAll(location => location.Id == id) > 0, "production.location-not-found", "production.location-removed");

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

    public Result<DateRange> AddUnavailableRange(Guid castId, DateTimeOffset start, DateTimeOffset end)
    {
        if (DateOnly.FromDateTime(end.LocalDateTime) < DateOnly.FromDateTime(start.LocalDateTime))
            return Result<DateRange>.Failure("production.invalid-date-range", "The end date must not precede the start date.");

        var document = projects.Snapshot();
        var member = document.ProductionInfo?.CastList.FirstOrDefault(candidate => candidate.Id == castId);
        if (member is null)
            return Result<DateRange>.Failure("production.cast-not-found", "The cast member does not exist.");

        var range = new DateRange { Start = start, End = end };
        member.UnavailableRanges.Add(range);
        member.UnavailableRanges = member.UnavailableRanges.OrderBy(value => value.Start).ToList();
        projects.Apply(document, "production.unavailability-added", structural: false);
        return Result<DateRange>.Success(range);
    }

    public Result<Unit> RemoveUnavailableRange(Guid castId, Guid rangeId)
    {
        var document = projects.Snapshot();
        var member = document.ProductionInfo?.CastList.FirstOrDefault(candidate => candidate.Id == castId);
        if (member is null)
            return Result<Unit>.Failure("production.cast-not-found", "The cast member does not exist.");
        if (member.UnavailableRanges.RemoveAll(range => range.Id == rangeId) == 0)
            return Result<Unit>.Failure("production.range-not-found", "The unavailable range does not exist.");
        projects.Apply(document, "production.unavailability-removed", structural: false);
        return Result<Unit>.Success(Unit.Value);
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

    private Result<Unit> RemoveFromRoster(Func<ProductionInfo, bool> remove, string errorCode, string changeKind)
    {
        var document = projects.Snapshot();
        document.ProductionInfo ??= new ProductionInfo();
        if (!remove(document.ProductionInfo))
            return Result<Unit>.Failure(errorCode, "The roster item does not exist.");
        projects.Apply(document, changeKind, structural: false);
        return Result<Unit>.Success(Unit.Value);
    }
}
