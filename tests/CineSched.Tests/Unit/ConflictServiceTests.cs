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

    [Fact]
    public void BlackoutScan_ReportsScheduledNormalScenesWithoutBlockingMove()
    {
        var projects = TestData.Project();
        var scene = TestData.Scene("1");
        projects.Update(document => document.AllScenes.Add(scene));
        var day = projects.GetSnapshot().Document.ShootDays[0];
        var scheduling = new SchedulingService(projects);
        scheduling.SetBlackout(new(day.Id, true));

        var move = scheduling.MoveScenes(new([scene.Id], null, day.Id, 0));
        var conflict = Assert.Single(new ConflictService(projects).ScanBlackouts());

        Assert.True(move.IsSuccess);
        Assert.Equal(scene.Id, conflict.SceneId);
        Assert.True(conflict.IsBlackout);
    }
}
