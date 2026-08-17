using System.Text;
using System.Text.Json;

namespace CineSched.Tests.Unit;

public sealed class ProjectServiceTests
{
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
    public async Task FailedSave_PreservesDirtyState()
    {
        var projects = TestData.Project();
        projects.Update(document => document.ProjectTitle = "Dirty");
        await using var destination = new ThrowingStream();

        var result = await projects.SaveAsync(destination);

        Assert.False(result.IsSuccess);
        Assert.True(projects.IsDirty);
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("No space");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("No space");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("No space"));
    }
}
