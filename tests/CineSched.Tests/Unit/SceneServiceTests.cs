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

    [Fact]
    public void EditScene_PreservesIdentityPositionAndIndependentBreakdownCategories()
    {
        var projects = TestData.Project();
        var first = TestData.Scene("1");
        var edited = TestData.Scene("2");
        edited.Props.Add("KEY");
        edited.Wardrobe.Add("COAT");
        edited.Sfx.Add("RAIN");
        edited.Vfx.Add("SKY");
        projects.Update(document => document.ShootDays[0].Scenes.AddRange([first, edited, TestData.Scene("3")]));
        var service = new SceneService(projects);
        var snapshot = projects.GetSnapshot().Document.ShootDays[0].Scenes[1];
        snapshot.Props = ["MAP"];

        var result = service.EditScene(snapshot);
        var scenes = projects.GetSnapshot().Document.ShootDays[0].Scenes;

        Assert.True(result.IsSuccess);
        Assert.Equal(edited.Id, scenes[1].Id);
        Assert.Equal(["MAP"], scenes[1].Props);
        Assert.Equal(["COAT"], scenes[1].Wardrobe);
        Assert.Equal(["RAIN"], scenes[1].Sfx);
        Assert.Equal(["SKY"], scenes[1].Vfx);
    }

    [Fact]
    public void DeleteScene_RemovesAScheduledSceneEverywhere()
    {
        var projects = TestData.Project();
        var scene = TestData.Scene("1");
        projects.Update(document => document.ShootDays[0].Scenes.Add(scene));

        var result = new SceneService(projects).DeleteScene(scene.Id);
        var document = projects.GetSnapshot().Document;

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(document.AllScenes, candidate => candidate.Id == scene.Id);
        Assert.DoesNotContain(document.ShootDays.SelectMany(day => day.Scenes), candidate => candidate.Id == scene.Id);
    }

    [Fact]
    public void InvalidCreate_DoesNotModifyProject()
    {
        var projects = TestData.Project();

        var result = new SceneService(projects).CreateScene(new SceneInput("Scene", Duration: "invalid", EstimatedTime: "15"));

        Assert.False(result.IsSuccess);
        Assert.Empty(projects.GetSnapshot().Document.AllScenes);
    }

    [Fact]
    public void SortAndSearch_AreQueriesAndDoNotReorderPersistence()
    {
        var projects = TestData.Project();
        var scenes = new[]
        {
            TestData.Scene("2", "EXT. ZOO - NIGHT", "BOB"),
            TestData.Scene("1", "INT. HOUSE - DAY", "ANA")
        };
        scenes[1].Summary = "Hidden clue";
        projects.Update(document =>
        {
            document.AllScenes.AddRange(scenes);
            document.ShootDays[0].Scenes.Add(TestData.Scene("3", "INT. LAB - DAY", "CLARA"));
        });
        var service = new SceneService(projects);
        var storedOrder = projects.GetSnapshot().Document.AllScenes.Select(scene => scene.Id).ToArray();

        foreach (var sort in Enum.GetValues<BoneyardSort>()) Assert.Equal(2, service.SortBoneyard(sort).Count);
        Assert.Equal(storedOrder, projects.GetSnapshot().Document.AllScenes.Select(scene => scene.Id));
        Assert.Single(service.SearchScenes("clue"));
        Assert.Single(service.SearchScenes("clara"));
        Assert.Single(service.SearchScenes("lab"));
    }
}
