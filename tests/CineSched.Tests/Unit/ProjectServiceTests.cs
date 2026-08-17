using System.Text;
using System.Text.Json;

namespace CineSched.Tests.Unit;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task OpenAsync_LoadsCompleteSwiftV450FixtureWithoutLosingExtendedData()
    {
        var projects = new ProjectService();
        await using var stream = File.OpenRead(TestData.Asset("Projects", "current-v4.5.0.cinesched"));

        var result = await projects.OpenAsync(stream);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = result.Value!;
        Assert.Equal("Reference Feature", project.ProjectTitle);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), project.AllScenes.Single().Id);
        Assert.Equal(["RAIN"], project.AllScenes.Single().Sfx);
        Assert.Equal(["SKY REPLACEMENT"], project.AllScenes.Single().Vfx);
        Assert.True(project.ShootDays.Single().IsBlackout);
        Assert.Contains(project.ShootDays.Single().Scenes, scene => scene is { IsAutoMeal: true, MealKind: MealKind.Lunch });
        Assert.Contains(project.ShootDays.Single().Scenes, scene => scene.IsCalendarEvent);
        Assert.NotNull(project.ProductionInfo!.ScheduleLock);
        Assert.Equal("Ada Actor", project.ProductionInfo.CastList.Single().ActorName);
        Assert.True(project.ShootDays.Single().CallSheet.HasData);
    }

    [Fact]
    public void NewProject_CreatesInclusiveDefaultRange()
    {
        var projects = new ProjectService();
        var now = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.FromHours(-4));

        var project = projects.NewProject(now);

        Assert.Equal(31, project.ShootDays.Count);
        Assert.Equal(new DateOnly(2026, 8, 16), DateOnly.FromDateTime(project.ShootDays[0].Date.Date));
        Assert.False(projects.IsDirty);
    }

    [Fact]
    public async Task OpenAsync_AcceptsSwiftDatesLegacyTopLevelAndCsvCast()
    {
        const string json = """
        {
          "allScenes": [{"id":"790f6957-c092-4708-9707-1cc9cb810122","title":"INT. CASA - DAY","sceneNumber":"1","duration":8,"estimatedTime":15,"dayNightType":"DAY","cast":"ANA, BOB"}],
          "shootDays": [{"id":"b89375fd-dcaf-40ff-8895-bf09be5d81d2","date":"2026-08-16T00:00:00-0400","scenes":[],"callSheet":{},"isBlackout":false}]
        }
        """;
        var projects = new ProjectService();

        var result = await projects.OpenAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Imported Project", result.Value!.ProjectTitle);
        Assert.Equal(["ANA", "BOB"], result.Value.AllScenes[0].Cast);
        Assert.Equal(TimeSpan.FromHours(-4), result.Value.ShootDays[0].Date.Offset);
    }

    [Fact]
    public async Task OpenAsync_AppliesDefaultsForMissingOptionalFields()
    {
        var projects = new ProjectService();
        await using var stream = File.OpenRead(TestData.Asset("Projects", "missing-optionals.cinesched"));

        var result = await projects.OpenAsync(stream);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var scene = result.Value!.AllScenes.Single();
        Assert.Equal(string.Empty, scene.SceneNumber);
        Assert.Empty(scene.Props);
        Assert.False(scene.IsBanner);
        Assert.False(result.Value.ShootDays.Single().IsBlackout);
        Assert.Equal("01:30 PM", result.Value.ProductionInfo!.DefaultLunchTime);
    }

    [Fact]
    public async Task OpenAsync_ReadsAppleReferenceDateSecondsCultureIndependently()
    {
        var projects = new ProjectService();
        await using var stream = File.OpenRead(TestData.Asset("Projects", "legacy-top-level.cinesched"));

        var result = await projects.OpenAsync(stream);

        var expected = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(808099200);
        Assert.Equal(expected, result.Value!.ShootDays.Single().Date);
    }

    [Fact]
    public async Task OpenAsync_TrimsLegacyCsvCastFixture()
    {
        var projects = new ProjectService();
        await using var stream = File.OpenRead(TestData.Asset("Projects", "legacy-cast-string.cinesched"));

        var result = await projects.OpenAsync(stream);

        Assert.Equal(["ANA", "LUIS", "EXTRA"], result.Value!.AllScenes.Single().Cast);
    }

    [Fact]
    public async Task SaveAsync_WritesSharedCamelCaseWireFormat()
    {
        var projects = TestData.Project();
        projects.Update(document => document.AllScenes.Add(TestData.Scene("4")));
        using var destination = new MemoryStream();

        var result = await projects.SaveAsync(destination);
        destination.Position = 0;
        using var json = await JsonDocument.ParseAsync(destination);

        Assert.True(result.IsSuccess);
        Assert.True(json.RootElement.TryGetProperty("allScenes", out _));
        Assert.True(json.RootElement.TryGetProperty("projectTitle", out _));
        Assert.False(json.RootElement.GetProperty("allScenes")[0].TryGetProperty("displayTitle", out _));
        Assert.False(projects.IsDirty);
    }

    [Fact]
    public async Task SaveAsync_WritesExactSwiftEnumValuesAndExcludesUiPreferences()
    {
        var projects = new ProjectService();
        await using (var source = File.OpenRead(TestData.Asset("Projects", "current-v4.5.0.cinesched")))
            Assert.True((await projects.OpenAsync(source)).IsSuccess);
        using var destination = new MemoryStream();

        await projects.SaveAsync(destination);
        var json = Encoding.UTF8.GetString(destination.ToArray());

        Assert.Contains("\"dayNightType\": \"NIGHT\"", json, StringComparison.Ordinal);
        Assert.Contains("\"bannerType\": \"Meal Break\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mealKind\": \"Lunch\"", json, StringComparison.Ordinal);
        Assert.Contains("2026-08-16T20:30:00-0400", json, StringComparison.Ordinal);
        Assert.DoesNotContain("language", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sidebar", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("colorMode", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedSave_PreservesDirtyState()
    {
        var projects = TestData.Project();
        projects.Update(document => document.ProjectTitle = "Dirty");
        await using var destination = new ThrowingStream();

        var result = await projects.SaveAsync(destination);

        Assert.False(result.IsSuccess);
        Assert.True(projects.IsDirty);
    }

    [Fact]
    public async Task SaveThatFinishesAfterANewerRevision_DoesNotClearDirtyState()
    {
        var projects = TestData.Project();
        projects.Update(document => document.ProjectTitle = "First");
        await using var destination = new BlockingStream();

        var save = projects.SaveAsync(destination).AsTask();
        await destination.Started;
        projects.Update(document => document.ProjectTitle = "Second");
        destination.Release();
        var result = await save;

        Assert.True(result.IsSuccess);
        Assert.True(projects.IsDirty);
        Assert.Equal("Second", projects.GetSnapshot().Document.ProjectTitle);
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("No space");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("No space");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("No space"));
    }

    private sealed class BlockingStream : MemoryStream
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _blocked;

        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _blocked, 1) == 0)
            {
                _started.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }

            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
