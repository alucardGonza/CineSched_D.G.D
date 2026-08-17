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
}
