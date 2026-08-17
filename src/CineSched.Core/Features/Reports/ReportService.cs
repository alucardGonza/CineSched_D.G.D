using CineSched.Core.Common;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.Scheduling;
using FontLibrary.libRoboto;
using System.Resources;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace CineSched.Core.Features.Reports;

public sealed class ReportService
{
    private static readonly object FontGate = new();
    private static bool _fontsConfigured;

    public async ValueTask<Result<Unit>> GenerateAsync(
        ReportKind kind,
        ReportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureFonts();
            using var document = new PdfDocument();
            document.Info.Title = $"{request.Project.ProjectTitle} — {kind}";
            document.Info.Creator = "CineSched";

            switch (kind)
            {
                case ReportKind.Schedule:
                    RenderSchedule(document, request);
                    break;
                case ReportKind.Stripboard:
                    RenderStripboard(document, request, includeTimes: false);
                    break;
                case ReportKind.ShootingSchedule:
                    RenderStripboard(document, request, includeTimes: true);
                    break;
                case ReportKind.DaysOutOfDays:
                    RenderDood(document, request);
                    break;
                case ReportKind.Breakdown:
                    RenderBreakdown(document, request);
                    break;
                case ReportKind.CallSheet:
                    RenderCallSheet(document, request);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            if (document.PageCount == 0)
            {
                return Result<Unit>.Failure("report.no-data", "There is no data for this report.");
            }

            document.Save(destination, closeStream: false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or ArgumentException)
        {
            return Result<Unit>.Failure(
                "report.generation-failed",
                exception.Message,
                new Dictionary<string, object?> { ["kind"] = kind.ToString(), ["exception"] = exception.GetType().Name });
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> CalculateDoodCodes(ReportRequest request)
    {
        var days = request.Project.ShootDays.OrderBy(day => day.Date).ToList();
        var roster = request.Project.ProductionInfo?.CastList ?? [];
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in roster)
        {
            var works = days.Select(day => day.Scenes.Any(scene => IsNormal(scene) && scene.Cast.Contains(
                member.CharacterName, StringComparer.OrdinalIgnoreCase))).ToArray();
            var first = Array.FindIndex(works, value => value);
            var last = Array.FindLastIndex(works, value => value);
            var codes = new List<string>(days.Count);
            for (var index = 0; index < days.Count; index++)
            {
                var unavailable = member.UnavailableRanges.Any(range => range.Contains(days[index].Date));
                if (unavailable)
                {
                    codes.Add("X");
                }
                else if (works[index])
                {
                    codes.Add(first == last ? "SWF" : index == first ? "SW" : index == last ? "WF" : "W");
                }
                else if (first >= 0 && index > first && index < last && request.IncludeHoldDays)
                {
                    codes.Add("H");
                }
                else
                {
                    codes.Add(string.Empty);
                }
            }

            result[member.DisplayString] = codes;
        }

        return result;
    }

    private static void RenderSchedule(PdfDocument document, ReportRequest request)
    {
        var rows = request.Project.ShootDays.OrderBy(day => day.Date)
            .Select(day => new[]
            {
                day.Date.ToString("yyyy-MM-dd"),
                day.IsBlackout ? Text(request, "Blackout", "No disponible") : string.Empty,
                string.Join(", ", day.Scenes.Where(IsNormal).Select(scene => scene.SceneNumber)),
                $"{day.TotalDuration / 8} {day.TotalDuration % 8}/8",
                $"{day.TotalEstimatedTime} min"
            }).ToList();
        RenderTable(document, request.Project.ProjectTitle, ["Date", "Status", "Scenes", "Pages", "Time"], rows, landscape: true);
    }

    private static void RenderStripboard(PdfDocument document, ReportRequest request, bool includeTimes)
    {
        var rows = new List<string[]>();
        foreach (var day in request.Project.ShootDays.OrderBy(day => day.Date))
        {
            var cursor = ParseTime(day.CallSheet.GeneralCallTime, day.Date) ?? day.Date.AddHours(7);
            foreach (var scene in day.Scenes)
            {
                var anchor = ParseTime(scene.CustomStartTime, day.Date);
                if (anchor > cursor) cursor = anchor.Value;
                var start = cursor;
                cursor = cursor.AddMinutes(Math.Max(0, scene.EstimatedTime));
                rows.Add(includeTimes
                    ? [day.Date.ToString("MMM dd"), start.ToString("hh:mm tt"), scene.DisplayTitle, FormatPages(scene), $"{scene.EstimatedTime} min"]
                    : [day.Date.ToString("MMM dd"), scene.IsBanner ? "BANNER" : scene.DayNightType.ToString().ToUpperInvariant(), scene.DisplayTitle, string.Join(", ", scene.Cast), FormatPages(scene)]);
            }
        }

        var title = includeTimes ? "One-Line Shooting Schedule" : "Strip Schedule";
        var headers = includeTimes ? new[] { "Day", "Time", "Scene / Slugline", "Pages", "Duration" } : new[] { "Day", "Type", "Scene / Slugline", "Cast", "Pages" };
        RenderTable(document, $"{request.Project.ProjectTitle} — {title}", headers, rows, landscape: true);
    }

    private void RenderDood(PdfDocument document, ReportRequest request)
    {
        var days = request.Project.ShootDays.OrderBy(day => day.Date).ToList();
        var codes = CalculateDoodCodes(request);
        const int maxDays = 12;
        const int maxActors = 24;
        var entries = codes.ToList();
        for (var actorStart = 0; actorStart < Math.Max(1, entries.Count); actorStart += maxActors)
        {
            for (var dayStart = 0; dayStart < Math.Max(1, days.Count); dayStart += maxDays)
            {
                var dayChunk = days.Skip(dayStart).Take(maxDays).ToList();
                var actorChunk = entries.Skip(actorStart).Take(maxActors).ToList();
                var headers = new List<string> { "Cast" };
                headers.AddRange(dayChunk.Select(day => day.Date.ToString("MM/dd") + (day.IsBlackout ? "*" : string.Empty)));
                var rows = actorChunk.Select(entry =>
                {
                    var cells = new List<string> { entry.Key };
                    cells.AddRange(entry.Value.Skip(dayStart).Take(dayChunk.Count));
                    return cells.ToArray();
                }).ToList();
                RenderTable(document, $"{request.Project.ProjectTitle} — Days Out of Days", headers.ToArray(), rows, landscape: true);
            }
        }
    }

    private static void RenderBreakdown(PdfDocument document, ReportRequest request)
    {
        var scenes = request.Project.AllScenes.Concat(request.Project.ShootDays.SelectMany(day => day.Scenes))
            .Where(IsNormal).GroupBy(scene => scene.Id).Select(group => group.First())
            .OrderBy(scene => SceneSortKey(scene.SceneNumber)).ToList();
        foreach (var scene in scenes)
        {
            var page = CreatePage(document, landscape: false);
            using var graphics = XGraphics.FromPdfPage(page);
            var y = DrawHeader(graphics, page, $"{request.Project.ProjectTitle} — Scene {scene.SceneNumber}", scene.Title);
            var sections = new (string Label, IEnumerable<string> Values)[]
            {
                ("Cast", scene.Cast), ("Extras", scene.Extras), ("Props", scene.Props),
                ("Set Dressing", scene.SetDressing), ("Wardrobe", scene.Wardrobe),
                ("Makeup / Hair", scene.MakeupHair), ("Vehicles", scene.Vehicles),
                ("Special Equipment", scene.SpecialEquipment), ("Stunts", scene.Stunts),
                ("SFX", scene.Sfx), ("VFX", scene.Vfx), ("Notes", [scene.BreakdownNotes])
            };
            foreach (var section in sections)
            {
                DrawLine(graphics, page, ref y, $"{section.Label}: {string.Join(", ", section.Values.Where(value => !string.IsNullOrWhiteSpace(value)))}");
            }
        }
    }

    private static void RenderCallSheet(PdfDocument document, ReportRequest request)
    {
        var day = request.ShootDay ?? request.Project.ShootDays.FirstOrDefault(candidate => candidate.CallSheet.HasData);
        if (day is null) return;
        var sheet = day.CallSheet;
        var production = request.Project.ProductionInfo ?? new ProductionInfo();
        var page = CreatePage(document, landscape: false);
        using var graphics = XGraphics.FromPdfPage(page);
        var y = DrawHeader(graphics, page, request.Project.ProjectTitle, $"{Text(request, "Call Sheet", "Hoja de llamado")} — {day.Date:dddd, MMMM d, yyyy}");
        var lines = new[]
        {
            $"{Text(request, "General Call", "Llamado general")}: {sheet.GeneralCallTime}",
            $"Director: {production.DirectorName} {production.DirectorPhone}",
            $"Producer: {production.ProducerName} {production.ProducerPhone}",
            $"1st AD: {production.AdName} {production.AdPhone}",
            $"Ready: {sheet.ReadyToShootTime}  Lunch: {sheet.LunchTime}  Wrap: {sheet.WrapTime}",
            $"Weather: {sheet.WeatherTemp} {sheet.WeatherCondition} {sheet.WeatherPrecipWind}",
            $"Hospital: {sheet.NearestHospital}",
            $"Locations: {string.Join("; ", sheet.Locations.Select(location => $"{location.Name} — {location.Address}"))}",
            $"Scenes: {string.Join(", ", day.Scenes.Where(IsNormal).Select(scene => scene.DisplayTitle))}",
            $"Cast: {string.Join("; ", sheet.CastCallEntries.Select(entry => $"{entry.ActorName} / {entry.CharacterName} / {entry.OnSetTime}"))}",
            $"Crew: {string.Join("; ", sheet.CrewCallEntries.Select(entry => $"{entry.Role}: {entry.Name} {entry.CallTime}"))}",
            $"Notes: {string.Join("; ", sheet.ProductionNotes.Append(sheet.Notes).Where(value => !string.IsNullOrWhiteSpace(value)))}"
        };
        foreach (var line in lines) DrawLine(graphics, page, ref y, line);
    }

    private static void RenderTable(PdfDocument document, string title, string[] headers, IReadOnlyList<string[]> rows, bool landscape)
    {
        const double margin = 32;
        var page = CreatePage(document, landscape);
        var graphics = XGraphics.FromPdfPage(page);
        var y = DrawHeader(graphics, page, title, string.Empty);
        var columnWidth = (page.Width.Point - (margin * 2)) / headers.Length;

        DrawTableRow(graphics, headers, y, columnWidth, bold: true);
        y += 22;
        foreach (var row in rows)
        {
            if (y > page.Height.Point - 45)
            {
                graphics.Dispose();
                page = CreatePage(document, landscape);
                graphics = XGraphics.FromPdfPage(page);
                y = DrawHeader(graphics, page, title, Text(null, "continued", "continuación"));
                DrawTableRow(graphics, headers, y, columnWidth, bold: true);
                y += 22;
            }

            DrawTableRow(graphics, row, y, columnWidth, bold: false);
            y += 20;
        }

        graphics.Dispose();
    }

    private static void DrawTableRow(XGraphics graphics, IReadOnlyList<string> cells, double y, double columnWidth, bool bold)
    {
        var font = new XFont("Roboto", bold ? 8 : 7, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        for (var index = 0; index < cells.Count; index++)
        {
            var x = 32 + (index * columnWidth);
            graphics.DrawRectangle(XPens.LightGray, x, y, columnWidth, 20);
            graphics.DrawString(TrimTo(cells[index] ?? string.Empty, 38), font, XBrushes.Black, new XRect(x + 3, y + 2, columnWidth - 6, 16), XStringFormats.CenterLeft);
        }
    }

    private static PdfPage CreatePage(PdfDocument document, bool landscape)
    {
        var page = document.AddPage();
        page.Size = PageSize.Letter;
        page.Orientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        return page;
    }

    private static double DrawHeader(XGraphics graphics, PdfPage page, string title, string subtitle)
    {
        graphics.DrawString(title, new XFont("Roboto", 16, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(32, 24, page.Width.Point - 64, 24), XStringFormats.TopLeft);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            graphics.DrawString(subtitle, new XFont("Roboto", 9), XBrushes.DarkSlateGray,
                new XRect(32, 48, page.Width.Point - 64, 18), XStringFormats.TopLeft);
        }

        return 72;
    }

    private static void DrawLine(XGraphics graphics, PdfPage page, ref double y, string text)
    {
        graphics.DrawString(TrimTo(text, 110), new XFont("Roboto", 9), XBrushes.Black,
            new XRect(32, y, page.Width.Point - 64, 18), XStringFormats.TopLeft);
        y += 20;
    }

    private static void EnsureFonts()
    {
        lock (FontGate)
        {
            if (_fontsConfigured) return;
            GlobalFontSettings.FontResolver = new EmbeddedRobotoFontResolver();
            _fontsConfigured = true;
        }
    }

    private static DateTimeOffset? ParseTime(string? value, DateTimeOffset date)
    {
        if (!DateTime.TryParse(value, out var parsed)) return null;
        return new DateTimeOffset(date.Year, date.Month, date.Day, parsed.Hour, parsed.Minute, 0, date.Offset);
    }

    private static string FormatPages(Scene scene) => scene.IsBanner || scene.IsCalendarEvent
        ? "—"
        : $"{scene.Duration / 8} {scene.Duration % 8}/8";

    private static bool IsNormal(Scene scene) => !scene.IsBanner && !scene.IsCalendarEvent;

    private static string SceneSortKey(string number) => string.Concat(number.Trim().PadLeft(12, '0'));

    private static string TrimTo(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";

    private static string Text(ReportRequest? request, string english, string spanish) =>
        request?.Language == ReportLanguage.Spanish ? spanish : english;

    private sealed class EmbeddedRobotoFontResolver : IFontResolver
    {
        private static readonly ResourceManager FontResources =
            new("FontLibrary.libRoboto.Resources", typeof(Roboto).Assembly);

        public byte[]? GetFont(string faceName)
        {
            var resourceName = faceName switch
            {
                "Roboto-BoldItalic" => "Roboto_BoldItalic",
                "Roboto-Bold" => "Roboto_Bold",
                "Roboto-Italic" => "Roboto_Italic",
                _ => "Roboto_Regular"
            };
            return FontResources.GetObject(resourceName) as byte[];
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var face = (isBold, isItalic) switch
            {
                (true, true) => "Roboto-BoldItalic",
                (true, false) => "Roboto-Bold",
                (false, true) => "Roboto-Italic",
                _ => "Roboto-Regular"
            };
            return new FontResolverInfo(face);
        }
    }
}
