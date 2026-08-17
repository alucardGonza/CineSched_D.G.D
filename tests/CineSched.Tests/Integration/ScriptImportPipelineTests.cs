using System.IO.Compression;
using System.Text;

namespace CineSched.Tests.Integration;

public sealed class ScriptImportPipelineTests
{
    [Fact]
    public async Task EquivalentFdxFountainAndHighland_ProduceEquivalentNormalizedScenes()
    {
        var fdx = """
            <FinalDraft><Content>
              <Paragraph Type="Scene Heading" Number="12A"><Text>INT. HOUSE - NIGHT</Text></Paragraph>
              <Paragraph Type="Character"><Text>ANA</Text></Paragraph>
              <Paragraph Type="Action"><Text>She opens the door.</Text></Paragraph>
            </Content></FinalDraft>
            """;
        var fountain = "INT. HOUSE - NIGHT #12A#\n\nANA\nHello.\n";
        var service = new ScriptImportService(TestData.Project());

        var fromFdx = await service.ImportAsync(Bytes(fdx), ".fdx");
        var fromFountain = await service.ImportAsync(Bytes(fountain), ".fountain");
        using var highland = Highland(fountain);
        var fromHighland = await service.ImportAsync(highland, ".highland");

        Assert.True(fromFdx.IsSuccess, fromFdx.Error?.Message);
        Assert.True(fromFountain.IsSuccess, fromFountain.Error?.Message);
        Assert.True(fromHighland.IsSuccess, fromHighland.Error?.Message);
        var normalized = new[] { fromFdx.Value!, fromFountain.Value!, fromHighland.Value! }
            .Select(result => result.Scenes.Single())
            .Select(scene => new { scene.SceneNumber, scene.Title, scene.DayNightType, Cast = string.Join('|', scene.Cast), scene.Duration, scene.EstimatedTime })
            .ToList();
        Assert.All(normalized, scene => Assert.Equal(normalized[0], scene));
    }

    private static MemoryStream Bytes(string value) => new(Encoding.UTF8.GetBytes(value));

    private static MemoryStream Highland(string fountain)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("text.fountain").Open(), Encoding.UTF8))
            writer.Write(fountain);
        stream.Position = 0;
        return stream;
    }
}
