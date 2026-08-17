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

    [Fact]
    public void DoodCodes_ExcludeHoldCellsWhenRequested()
    {
        var projects = TestData.Project(days: 3);
        projects.Update(document =>
        {
            document.ProductionInfo!.CastList.Add(new CastMember { ActorName = "Actor", CharacterName = "ANA" });
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: "ANA"));
            document.ShootDays[2].Scenes.Add(TestData.Scene("2", cast: "ANA"));
        });

        var codes = new ReportService().CalculateDoodCodes(new(
            projects.GetSnapshot().Document, ReportLanguage.English, IncludeHoldDays: false)).Single().Value;

        Assert.Equal(["SW", "", "WF"], codes);
    }

    [Fact]
    public async Task Dood_PaginatesActorsAndDaysAndRepeatsLandscapePages()
    {
        var projects = TestData.Project(days: 13);
        projects.Update(document =>
        {
            for (var index = 0; index < 25; index++)
                document.ProductionInfo!.CastList.Add(new CastMember { ActorName = $"Actor {index}", CharacterName = $"C{index}" });
        });
        using var stream = new MemoryStream();

        var result = await new ReportService().GenerateAsync(ReportKind.DaysOutOfDays,
            new(projects.GetSnapshot().Document, ReportLanguage.English), stream);
        stream.Position = 0;
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(4, pdf.PageCount);
        Assert.All(pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>(), page => Assert.True(page.Width.Point > page.Height.Point));
    }

    [Fact]
    public async Task Breakdown_CreatesOnePortraitPagePerNormalSceneOnly()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            document.AllScenes.Add(TestData.Scene("2"));
            document.ShootDays[0].Scenes.Add(TestData.Scene("1"));
            document.ShootDays[0].Scenes.Add(new Scene { Title = "Lunch", IsBanner = true });
            document.ShootDays[0].Scenes.Add(new Scene { Title = "Meeting", IsCalendarEvent = true });
        });
        using var stream = new MemoryStream();

        var result = await new ReportService().GenerateAsync(ReportKind.Breakdown,
            new(projects.GetSnapshot().Document, ReportLanguage.English), stream);
        stream.Position = 0;
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, pdf.PageCount);
        Assert.All(pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>(), page => Assert.True(page.Height.Point > page.Width.Point));
    }

    [Fact]
    public async Task CallSheet_IsPortraitAndUsesCompleteSynchronizedData()
    {
        var projects = TestData.Project();
        projects.Update(document =>
        {
            var member = new CastMember { CharacterName = "ANA", ActorName = "Updated Actor" };
            document.ProductionInfo!.CastList.Add(member);
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: "ANA"));
            document.ShootDays[0].CallSheet.GeneralCallTime = "07:30 AM";
            document.ShootDays[0].CallSheet.CastCallEntries.Add(new CastCallEntry { CharacterName = "ANA", ActorName = "Old Actor" });
        });
        using var stream = new MemoryStream();
        var day = projects.GetSnapshot().Document.ShootDays[0];

        var result = await new ReportService().GenerateAsync(ReportKind.CallSheet,
            new(projects.GetSnapshot().Document, ReportLanguage.Spanish, ShootDay: day), stream);
        stream.Position = 0;
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(pdf.Pages);
        Assert.True(pdf.Pages[0].Height.Point > pdf.Pages[0].Width.Point);
    }
}
