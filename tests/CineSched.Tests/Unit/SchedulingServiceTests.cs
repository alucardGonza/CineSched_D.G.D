namespace CineSched.Tests.Unit;

public sealed class SchedulingServiceTests
{
    [Fact]
    public void CalendarWeeks_AreAlignedMondayThroughSunday()
    {
        var projects = TestData.Project(new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero), days: 7);

        var weeks = new SchedulingService(projects).GetCalendarWeeks();

        Assert.All(weeks, week => Assert.Equal(7, week.Count));
        Assert.Equal(DayOfWeek.Monday, weeks[0][0].Date.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, weeks[^1][6].Date.DayOfWeek);
        Assert.False(weeks[0][0].IsInRange);
        Assert.Null(weeks[0][0].ShootDay);
    }

    [Fact]
    public void MoveReorderAndRemoveScenes_PreserveIdentityAndOrder()
    {
        var projects = TestData.Project();
        var scenes = new[] { TestData.Scene("1"), TestData.Scene("2"), TestData.Scene("3") };
        projects.Update(document => document.AllScenes.AddRange(scenes));
        var service = new SchedulingService(projects);
        var day = projects.GetSnapshot().Document.ShootDays[0];

        service.MoveScenes(new MoveScenesRequest([scenes[1].Id, scenes[2].Id], null, day.Id, 0));
        service.ReorderScenes(new ReorderScenesRequest(day.Id, [scenes[2].Id], 0));
        service.RemoveFromDay(day.Id, [scenes[1].Id]);
        var result = projects.GetSnapshot().Document;

        Assert.Equal([scenes[2].Id], result.ShootDays[0].Scenes.Select(scene => scene.Id));
        Assert.Contains(result.AllScenes, scene => scene.Id == scenes[1].Id);
        Assert.Equal(3, result.AllScenes.Count + result.ShootDays.Sum(value => value.Scenes.Count));
    }

    [Fact]
    public void MoveWholeDay_SwapsContentWithoutChangingContainerDates()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            document.ShootDays[0].Scenes.Add(TestData.Scene("1"));
            document.ShootDays[0].CallSheet.GeneralCallTime = "07:30 AM";
            document.ShootDays[1].Scenes.Add(TestData.Scene("2"));
        });
        var before = projects.GetSnapshot().Document;
        var firstDate = before.ShootDays[0].Date;
        var secondDate = before.ShootDays[1].Date;

        new SchedulingService(projects).MoveWholeDay(new(before.ShootDays[0].Id, before.ShootDays[1].Id));
        var after = projects.GetSnapshot().Document;

        Assert.Equal(firstDate, after.ShootDays[0].Date);
        Assert.Equal(secondDate, after.ShootDays[1].Date);
        Assert.Equal("2", after.ShootDays[0].Scenes.Single().SceneNumber);
        Assert.Equal("07:30 AM", after.ShootDays[1].CallSheet.GeneralCallTime);
    }

    [Fact]
    public void ChangeDateRange_ShiftsContentOrPreservesAbsoluteDate()
    {
        var projects = TestData.Project(days: 3);
        var service = new SchedulingService(projects);
        var original = projects.GetSnapshot().Document.ShootDays[1];
        projects.Update(document => document.ShootDays[1].Scenes.Add(TestData.Scene("7")));

        service.ChangeDateRange(new(original.Date.AddDays(2), original.Date.AddDays(4), ShiftExisting: true));
        var shifted = projects.GetSnapshot().Document;

        Assert.Contains(shifted.ShootDays, day => day.Date.Date == original.Date.AddDays(3).Date && day.Scenes.Any(scene => scene.SceneNumber == "7"));
    }

    [Fact]
    public void ChangeDateRangeWithoutShift_PreservesAbsoluteDatesAndReturnsExcludedScenesToBoneyard()
    {
        var projects = TestData.Project(days: 4);
        projects.Update(document =>
        {
            document.ShootDays[1].Scenes.Add(TestData.Scene("1"));
            document.ShootDays[3].Scenes.Add(TestData.Scene("2"));
        });
        var before = projects.GetSnapshot().Document;
        var keptDate = before.ShootDays[1].Date;

        new SchedulingService(projects).ChangeDateRange(new(
            before.ShootDays[1].Date, before.ShootDays[2].Date, ShiftExisting: false));
        var after = projects.GetSnapshot().Document;

        Assert.Contains(after.ShootDays, day => day.Date == keptDate && day.Scenes.Any(scene => scene.SceneNumber == "1"));
        Assert.Contains(after.AllScenes, scene => scene.SceneNumber == "2");
    }

    [Fact]
    public void MoveGroup_PreservesRelativeDocumentOrderAndMovesExactlyOnce()
    {
        var projects = TestData.Project();
        var scenes = new[] { TestData.Scene("1"), TestData.Scene("2"), TestData.Scene("3") };
        projects.Update(document => document.AllScenes.AddRange(scenes));
        var day = projects.GetSnapshot().Document.ShootDays[0];

        new SchedulingService(projects).MoveScenes(new([scenes[2].Id, scenes[0].Id], null, day.Id, 0));
        var result = projects.GetSnapshot().Document;

        Assert.Equal([scenes[0].Id, scenes[2].Id], result.ShootDays[0].Scenes.Select(scene => scene.Id));
        Assert.Equal(3, result.AllScenes.Count + result.ShootDays.SelectMany(value => value.Scenes).Count());
    }

    [Fact]
    public void SetBlackout_AppliesOnlyMatchingWeekday()
    {
        var projects = TestData.Project(days: 15);
        var days = projects.GetSnapshot().Document.ShootDays;
        var saturday = days.First(day => day.Date.DayOfWeek == DayOfWeek.Saturday);

        new SchedulingService(projects).SetBlackout(new(saturday.Id, true, ApplyToMatchingWeekday: true));
        var result = projects.GetSnapshot().Document.ShootDays;

        Assert.All(result.Where(day => day.Date.DayOfWeek == DayOfWeek.Saturday), day => Assert.True(day.IsBlackout));
        Assert.All(result.Where(day => day.Date.DayOfWeek != DayOfWeek.Saturday), day => Assert.False(day.IsBlackout));
    }

    [Fact]
    public void UndoRedo_TracksStructuralChangesAndNewChangeClearsRedo()
    {
        var projects = TestData.Project();
        projects.Update(document => document.AllScenes.Add(TestData.Scene("1")));
        var service = new SchedulingService(projects);
        var snapshot = projects.GetSnapshot().Document;
        var scene = snapshot.AllScenes.Single();
        service.MoveScenes(new([scene.Id], null, snapshot.ShootDays[0].Id, 0));

        Assert.True(service.Undo().IsSuccess);
        Assert.Single(projects.GetSnapshot().Document.AllScenes);
        Assert.True(service.Redo().IsSuccess);
        Assert.Single(projects.GetSnapshot().Document.ShootDays[0].Scenes);
        Assert.True(service.Undo().IsSuccess);
        service.SetBlackout(new(projects.GetSnapshot().Document.ShootDays[0].Id, true));
        Assert.False(service.Redo().IsSuccess);
    }

    [Fact]
    public void UndoHistory_KeepsOnlyThirtyStructuralSnapshots()
    {
        var projects = TestData.Project();
        var service = new SchedulingService(projects);
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;
        for (var index = 0; index < 35; index++)
            service.SetBlackout(new(dayId, index % 2 == 0));

        var successfulUndos = 0;
        while (service.Undo().IsSuccess) successfulUndos++;

        Assert.Equal(30, successfulUndos);
    }
}
