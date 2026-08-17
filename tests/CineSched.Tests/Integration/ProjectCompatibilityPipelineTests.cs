using System.Text;

namespace CineSched.Tests.Integration;

public sealed class ProjectCompatibilityPipelineTests
{
    [Fact]
    public async Task SwiftFixture_OpenModifySaveReload_PreservesSharedData()
    {
        const string fixture = """
        {"allScenes":[{"id":"590f6957-c092-4708-9707-1cc9cb810122","title":"INT. CASA - DAY","sceneNumber":"1","duration":8,"estimatedTime":30,"dayNightType":"DAY","cast":["ANA"],"props":["KEY"],"sfx":["RAIN"],"vfx":["SKY"]}],"shootDays":[],"projectTitle":"Feature","createdDate":"2026-08-01T10:00:00-0400","isShiftModeEnabled":false,"productionInfo":{}}
        """;
        var first = new ProjectService();
        Assert.True((await first.OpenAsync(new MemoryStream(Encoding.UTF8.GetBytes(fixture)))).IsSuccess);
        var scene = first.GetSnapshot().Document.AllScenes.Single();
        scene.Wardrobe.Add("COAT");
        new SceneService(first).EditScene(scene);
        using var saved = new MemoryStream();
        Assert.True((await first.SaveAsync(saved)).IsSuccess);

        saved.Position = 0;
        var second = new ProjectService();
        var reopened = await second.OpenAsync(saved);

        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        var loaded = reopened.Value!.AllScenes.Single();
        Assert.Equal(["KEY"], loaded.Props);
        Assert.Equal(["COAT"], loaded.Wardrobe);
        Assert.Equal(["RAIN"], loaded.Sfx);
        Assert.Equal(["SKY"], loaded.Vfx);
    }
}
