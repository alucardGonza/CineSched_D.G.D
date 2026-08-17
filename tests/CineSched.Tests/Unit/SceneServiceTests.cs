namespace CineSched.Tests.Unit;

public sealed class SceneServiceTests
{
    [Theory]
    [InlineData("15", 15)]
    [InlineData("8", 8)]
    [InlineData("1 7/8", 15)]
    [InlineData("7/8", 7)]
    [InlineData("2.5", 20)]
    public void ParseDuration_ConvertsPagesToEighths(string input, int expected)
    {
        var service = new SceneService(TestData.Project());
        Assert.Equal(expected, service.ParseDuration(input).Value);
    }

    [Theory]
    [InlineData("4", 240)]
    [InlineData("10", 600)]
    [InlineData("15", 15)]
    [InlineData("2:30", 150)]
    public void ParseEstimatedTime_ConvertsSupportedFormats(string input, int expected)
    {
        var service = new SceneService(TestData.Project());
        Assert.Equal(expected, service.ParseEstimatedTime(input).Value);
    }

    [Fact]
    public void CreateScene_ExtractsNumberAndDuplicateGetsNewIdentity()
    {
        var projects = TestData.Project();
        var service = new SceneService(projects);

        var created = service.CreateScene(new SceneInput("12A. INT. CASA - DAY", Duration: "8", EstimatedTime: "15"));
        var duplicate = service.DuplicateScene(created.Value!.Id);

        Assert.Equal("12A", created.Value.SceneNumber);
        Assert.Equal("INT. CASA - DAY", created.Value.Title);
        Assert.NotEqual(created.Value.Id, duplicate.Value!.Id);
    }

    [Fact]
    public void ScriptOrder_CoversScheduledAndBoneyardScenes()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            document.AllScenes.AddRange([TestData.Scene("12B"), TestData.Scene("2"), TestData.Scene("20")]);
            document.ShootDays[0].Scenes.AddRange([TestData.Scene("12A"), TestData.Scene("12")]);
        });

        var ordered = new SceneService(projects).GetScriptOrder();

        Assert.Equal(["2", "12", "12A", "12B", "20"], ordered.Select(scene => scene.SceneNumber));
    }
}
