namespace CineSched.Tests.Unit;

public sealed class StripboardServiceTests
{
    [Fact]
    public void Timeline_CascadesFromCallAndHonorsNonRegressiveAnchor()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            var day = document.ShootDays[0];
            day.CallSheet.GeneralCallTime = "07:30 AM";
            day.Scenes.Add(new Scene { Title = "A", EstimatedTime = 60 });
            day.Scenes.Add(new Scene { Title = "B", EstimatedTime = 30, CustomStartTime = "11:00 AM" });
            day.Scenes.Add(new Scene { Title = "C", EstimatedTime = 15, CustomStartTime = "10:00 AM" });
        });

        var result = new StripboardService(projects).CalculateTimeline(projects.GetSnapshot().Document.ShootDays[0].Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("07:30", result.Value!.Items[0].Start.ToString("HH:mm"));
        Assert.Equal("11:00", result.Value.Items[1].Start.ToString("HH:mm"));
        Assert.Equal(result.Value.Items[1].End, result.Value.Items[2].Start);
        Assert.Equal("11:45", result.Value.WrapTime!.Value.ToString("HH:mm"));
    }

    [Theory]
    [InlineData(MealKind.Snack, 15)]
    [InlineData(MealKind.Lunch, 60)]
    [InlineData(MealKind.Dinner, 60)]
    [InlineData(MealKind.Wrap, 60)]
    public void AddMeal_UsesCompatibleDefaults(MealKind kind, int expected)
    {
        var projects = TestData.Project();
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;

        var meal = new StripboardService(projects).AddMeal(dayId, kind, "1:00 PM");

        Assert.Equal(expected, meal.Value!.EstimatedTime);
        Assert.True(meal.Value.IsBanner);
        Assert.True(meal.Value.IsAutoMeal);
    }

    [Fact]
    public void BannerAndCalendarEvent_ParticipateInTimelineButNotScenePageTotalsOrBoneyard()
    {
        var projects = TestData.Project();
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;
        var service = new StripboardService(projects);
        service.AddBanner(dayId, BannerType.Notice, "Notice", "Note", 20, "#123456");
        service.AddCalendarEvent(dayId, "Meeting", "10:00 AM", "#654321", 10);

        var day = projects.GetSnapshot().Document.ShootDays[0];
        var timeline = service.CalculateTimeline(dayId);

        Assert.Equal(2, timeline.Value!.Items.Count);
        Assert.Equal(0, day.TotalDuration);
        Assert.Empty(projects.GetSnapshot().Document.AllScenes);
        Assert.Contains(day.Scenes, scene => scene.IsBanner);
        Assert.Contains(day.Scenes, scene => scene.IsCalendarEvent);
    }
}
