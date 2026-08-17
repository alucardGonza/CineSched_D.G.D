using PdfSharp.Pdf.IO;

namespace CineSched.Tests.Integration;

public sealed class ReportGenerationPipelineTests
{
    [Fact]
    public async Task RepresentativeProject_GeneratesAllSixReadablePdfs()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            var scene = TestData.Scene("1", cast: "ANA");
            scene.Props.Add("Key");
            document.AllScenes.Add(TestData.Scene("2"));
            document.ShootDays[0].Scenes.Add(scene);
            document.ShootDays[0].CallSheet.GeneralCallTime = "07:30 AM";
            document.ShootDays[0].CallSheet.Notes = "Safety first";
            document.ProductionInfo!.CastList.Add(new CastMember { CharacterName = "ANA", ActorName = "Actor" });
        });
        var report = new ReportService();
        var project = projects.GetSnapshot().Document;

        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            using var stream = new MemoryStream();
            var result = await report.GenerateAsync(kind,
                new ReportRequest(project, ReportLanguage.English, ShootDay: project.ShootDays[0]), stream);
            Assert.True(result.IsSuccess, $"{kind}: {result.Error?.Message}");
            stream.Position = 0;
            using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            Assert.True(pdf.PageCount > 0, kind.ToString());
        }
    }
}
