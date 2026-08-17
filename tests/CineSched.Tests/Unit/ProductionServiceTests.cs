namespace CineSched.Tests.Unit;

public sealed class ProductionServiceTests
{
    [Fact]
    public void NormalizeRoster_DeduplicatesIgnoringCaseAndWhitespace()
    {
        var projects = TestData.Project();
        projects.Update(document => document.ProductionInfo!.LocationRoster.AddRange([
            new Location { Name = " Studio ", Address = " Main St " },
            new Location { Name = "studio", Address = "main st" }
        ]));

        var result = new ProductionService(projects).NormalizeRoster();

        Assert.Single(result);
    }

    [Fact]
    public void UpdateCast_PreservesStableIdentity()
    {
        var projects = TestData.Project();
        var member = new CastMember { CharacterName = "ANA", ActorName = "Old" };
        projects.Update(document => document.ProductionInfo!.CastList.Add(member));

        var updated = new ProductionService(projects).UpdateCast(member.Id, "New", "ANA");

        Assert.Equal(member.Id, updated.Value!.Id);
        Assert.Equal("New", projects.GetSnapshot().Document.ProductionInfo!.CastList.Single().ActorName);
    }

    [Fact]
    public void AddAndRemoveRosterItems_UseStableIdsAndDeduplicateLocations()
    {
        var projects = TestData.Project();
        var service = new ProductionService(projects);
        var cast = service.AddCast("Actor", "ANA");
        var crew = service.AddCrew("Grip", "Key Grip", "555", true);
        var firstLocation = service.AddLocation(" Stage ", " Main ");
        var sameLocation = service.AddLocation("stage", "main");

        Assert.Equal(firstLocation.Id, sameLocation.Id);
        Assert.True(service.RemoveCast(cast.Id).IsSuccess);
        Assert.True(service.RemoveCrew(crew.Id).IsSuccess);
        Assert.True(service.RemoveLocation(firstLocation.Id).IsSuccess);
        var production = projects.GetSnapshot().Document.ProductionInfo!;
        Assert.Empty(production.CastList);
        Assert.Empty(production.Crew);
        Assert.Empty(production.LocationRoster);
    }

    [Fact]
    public void UnavailableRanges_AreValidatedOrderedAndRemovable()
    {
        var projects = TestData.Project();
        var service = new ProductionService(projects);
        var cast = service.AddCast("Actor", "ANA");
        var later = service.AddUnavailableRange(cast.Id,
            new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var earlier = service.AddUnavailableRange(cast.Id,
            new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero));
        var invalid = service.AddUnavailableRange(cast.Id,
            new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero));

        Assert.True(later.IsSuccess);
        Assert.True(earlier.IsSuccess);
        Assert.False(invalid.IsSuccess);
        var ranges = projects.GetSnapshot().Document.ProductionInfo!.CastList.Single().UnavailableRanges;
        Assert.Equal([17, 20], ranges.Select(range => range.Start.Day));
        Assert.True(service.RemoveUnavailableRange(cast.Id, earlier.Value!.Id).IsSuccess);
        Assert.Single(projects.GetSnapshot().Document.ProductionInfo!.CastList.Single().UnavailableRanges);
    }
}
