using PdfSharp.Pdf.IO;

namespace CineSched.Tests.Unit;

public sealed class ReportServiceTests
{
    [Fact]
    public void DoodCodes_ProduceStartWorkHoldFinishAndUnavailable()
    {
        var projects = TestData.Project(days: 5);
        projects.Update(document =>
        {
            var member = new CastMember { ActorName = "Actor", CharacterName = "ANA" };
            member.UnavailableRanges.Add(new DateRange { Start = document.ShootDays[4].Date, End = document.ShootDays[4].Date });
            document.ProductionInfo!.CastList.Add(member);
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: "ANA"));
            document.ShootDays[2].Scenes.Add(TestData.Scene("2", cast: "ANA"));
        });
        var request = new ReportRequest(projects.GetSnapshot().Document, ReportLanguage.English);

        var codes = new ReportService().CalculateDoodCodes(request).Single().Value;

        Assert.Equal(["SW", "H", "WF", "", "X"], codes);
    }

    [Fact]
    public async Task SchedulePdf_IsLetterLandscapeAndReopens()
    {
        var projects = TestData.Project();
        projects.Update(document => document.ShootDays[0].Scenes.Add(TestData.Scene("1")));
        using var stream = new MemoryStream();

        var result = await new ReportService().GenerateAsync(
            ReportKind.Schedule,
            new ReportRequest(projects.GetSnapshot().Document, ReportLanguage.English),
            stream);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(stream.ToArray(), 0, 4));
        stream.Position = 0;
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(pdf.Pages[0].Width.Point > pdf.Pages[0].Height.Point);
    }
}
