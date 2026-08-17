using System.Text;

namespace CineSched.Tests.Integration;

public sealed class ProjectCompatibilityPipelineTests
{
    [Theory]
    [InlineData("current-v4.5.0.cinesched")]
    [InlineData("legacy-top-level.cinesched")]
    [InlineData("legacy-cast-string.cinesched")]
    [InlineData("missing-optionals.cinesched")]
    public async Task SwiftFixtures_OpenModifySaveReload_PreserveIdsDatesAndUnmodifiedFields(string fixtureName)
    {
        var first = new ProjectService();
        await using var source = File.OpenRead(TestData.Asset("Projects", fixtureName));
        Assert.True((await first.OpenAsync(source)).IsSuccess);
        var scene = first.GetSnapshot().Document.AllScenes.Single();
        var originalId = scene.Id;
        var originalDate = first.GetSnapshot().Document.ShootDays.FirstOrDefault()?.Date;
        scene.Wardrobe.Add("COAT");
        new SceneService(first).EditScene(scene);
        using var saved = new MemoryStream();
        Assert.True((await first.SaveAsync(saved)).IsSuccess);

        saved.Position = 0;
        var second = new ProjectService();
        var reopened = await second.OpenAsync(saved);

        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        var loaded = reopened.Value!.AllScenes.Single();
        Assert.Equal(originalId, loaded.Id);
        Assert.Contains("COAT", loaded.Wardrobe);
        Assert.Equal(originalDate, reopened.Value.ShootDays.FirstOrDefault()?.Date);
        if (fixtureName == "current-v4.5.0.cinesched")
        {
            Assert.Equal(["KEY"], loaded.Props);
            Assert.Equal(["RAIN"], loaded.Sfx);
            Assert.Equal(["SKY REPLACEMENT"], loaded.Vfx);
            Assert.NotNull(reopened.Value.ProductionInfo!.ScheduleLock);
        }
    }
}
