namespace CineSched.Tests.Unit;

public sealed class ScheduleLockServiceTests
{
    [Fact]
    public void LockAndChanges_CaptureAddedAndRemovedWorkingDays()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: "ANA"));
            document.ShootDays[1].Scenes.Add(TestData.Scene("2", cast: "BOB"));
        });
        var service = new ScheduleLockService(projects);
        var locked = service.Lock(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));

        projects.Update(document =>
        {
            document.ShootDays[0].Scenes.Clear();
            document.ShootDays[2].Scenes.Add(TestData.Scene("3", cast: "ANA"));
        });
        var change = Assert.Single(service.GetChanges(), item => item.ActorDisplayName == "ANA");

        Assert.Single(locked.WorkingDays["ANA"]);
        Assert.Single(change.AddedDays);
        Assert.Single(change.RemovedDays);
        service.Unlock();
        Assert.Empty(service.GetChanges());
    }
}
