using System.Text;

namespace CineSched.Tests.Integration;

public sealed class ProjectWorkflowPipelineTests
{
    [Fact]
    public async Task ImportScheduleLockChangeUndoPersist_CompletesWorkflow()
    {
        var projects = TestData.Project();
        var imports = new ScriptImportService(projects);
        var parsed = await imports.ImportAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("INT. HOME - DAY #1#\n\nANA\nHello")), ".fountain");
        imports.Commit(parsed.Value!);
        var schedule = new SchedulingService(projects);
        var snapshot = projects.GetSnapshot().Document;
        schedule.MoveScenes(new([snapshot.AllScenes[0].Id], null, snapshot.ShootDays[0].Id, 0));
        new ScheduleLockService(projects).Lock(DateTimeOffset.UtcNow);
        schedule.MoveScenes(new([projects.GetSnapshot().Document.ShootDays[0].Scenes[0].Id],
            projects.GetSnapshot().Document.ShootDays[0].Id, projects.GetSnapshot().Document.ShootDays[1].Id, 0));
        Assert.True(schedule.Undo().IsSuccess);
        using var stream = new MemoryStream();
        Assert.True((await projects.SaveAsync(stream)).IsSuccess);
        stream.Position = 0;
        var reloaded = new ProjectService();

        Assert.True((await reloaded.OpenAsync(stream)).IsSuccess);
        Assert.Single(reloaded.GetSnapshot().Document.ShootDays[0].Scenes);
        Assert.NotNull(reloaded.GetSnapshot().Document.ProductionInfo!.ScheduleLock);
    }
}
