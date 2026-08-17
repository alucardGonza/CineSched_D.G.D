using System.IO.Compression;
using System.Text;

namespace CineSched.Tests.Unit;

public sealed class ScriptImportServiceTests
{
    [Fact]
    public async Task Fountain_ParsesSceneHeadingCastAndNumberWithoutMutatingUntilCommit()
    {
        var projects = TestData.Project();
        var service = new ScriptImportService(projects);
        const string fountain = """
            INT. HOUSE - DAY #12A#

            ANA
            Hello.

            EXT. STREET - NIGHT #13#
            """;

        var parsed = await service.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(fountain)), ".fountain");

        Assert.True(parsed.IsSuccess, parsed.Error?.Message);
        Assert.Empty(projects.GetSnapshot().Document.AllScenes);
        Assert.Equal("12A", parsed.Value!.Scenes[0].SceneNumber);
        Assert.Contains("ANA", parsed.Value.Scenes[0].Cast);
        Assert.Equal(DayNightType.Night, parsed.Value.Scenes[1].DayNightType);
        service.Commit(parsed.Value);
        Assert.Equal(2, projects.GetSnapshot().Document.AllScenes.Count);
    }

    [Fact]
    public async Task FinalDraft_ParsesSceneHeadingAndCharacter()
    {
        const string fdx = """
        <FinalDraft><Content>
          <Paragraph Type="Scene Heading" Number="7"><Text>INT. LAB - NIGHT</Text></Paragraph>
          <Paragraph Type="Character"><Text>DR. ANA (V.O.)</Text></Paragraph>
        </Content></FinalDraft>
        """;
        var service = new ScriptImportService(TestData.Project());

        var result = await service.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(fdx)), ".fdx");

        Assert.Equal("7", result.Value!.Scenes.Single().SceneNumber);
        Assert.Equal(["DR. ANA"], result.Value.Scenes.Single().Cast);
    }

    [Fact]
    public async Task Highland_UsesPreferredTextEntryAndRejectsMissingText()
    {
        var service = new ScriptImportService(TestData.Project());
        using var valid = CreateArchive("text.fountain", "INT. ROOM - DAY #1#");
        using var invalid = CreateArchive("notes.json", "{}");

        var success = await service.ImportAsync(valid, ".highland");
        var failure = await service.ImportAsync(invalid, ".highland");

        Assert.True(success.IsSuccess);
        Assert.Equal("import.invalid-highland", failure.Error!.Code);
    }

    private static MemoryStream CreateArchive(string name, string content)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8))
            writer.Write(content);
        stream.Position = 0;
        return stream;
    }
}
