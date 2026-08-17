namespace CineSched.Tests.Unit;

public sealed class ConflictServiceTests
{
    [Fact]
    public void ScanAvailability_ReturnsKnownUnavailableActorOnly()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            var date = document.ShootDays[0].Date;
            document.ProductionInfo!.CastList.Add(new CastMember
            {
                ActorName = "Actor",
                CharacterName = "ANA",
                UnavailableRanges = [new DateRange { Start = date, End = date }]
            });
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: ["ANA", "UNKNOWN"]));
        });

        var conflicts = new ConflictService(projects).ScanAvailability();

        var conflict = Assert.Single(conflicts);
        Assert.Equal("Actor", conflict.ActorName);
        Assert.Equal("ANA", conflict.CharacterName);
    }

    [Fact]
    public void DuplicateNumbers_AreGlobalAcrossBoneyardAndSchedule()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            document.AllScenes.Add(TestData.Scene("12A"));
            document.ShootDays[0].Scenes.Add(TestData.Scene("12a"));
        });

        var duplicates = new ConflictService(projects).ScanDuplicateSceneNumbers();

        Assert.Equal(2, duplicates["12A"].Count);
    }
}
