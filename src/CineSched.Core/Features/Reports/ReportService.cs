using CineSched.Core.Common;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.Scheduling;
using FontLibrary.libRoboto;
using System.Globalization;
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
                day.Date.ToString("d", ReportCulture(request)),
                day.IsBlackout ? Text(request, "Blackout", "No disponible") : string.Empty,
                string.Join(", ", day.Scenes.Where(IsNormal).Select(scene => scene.SceneNumber)),
                $"{day.TotalDuration / 8} {day.TotalDuration % 8}/8",
                $"{day.TotalEstimatedTime} min"
            }).ToList();
        RenderTable(document, request.Project.ProjectTitle,
            [Text(request, "Date", "Fecha"), Text(request, "Status", "Estado"), Text(request, "Scenes", "Escenas"), Text(request, "Pages", "Páginas"), Text(request, "Time", "Tiempo")],
            rows, landscape: true);
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
                    ? [day.Date.ToString("MMM dd", ReportCulture(request)), start.ToString("t", ReportCulture(request)), scene.DisplayTitle, FormatPages(scene), $"{scene.EstimatedTime} min"]
                    : [day.Date.ToString("MMM dd", ReportCulture(request)), scene.IsBanner ? "BANNER" : scene.DayNightType.ToString().ToUpperInvariant(), scene.DisplayTitle, string.Join(", ", scene.Cast), FormatPages(scene)]);
            }
        }

        var title = includeTimes ? Text(request, "One-Line Shooting Schedule", "Plan de rodaje One-Line") : Text(request, "Strip Schedule", "Plan de tiras");
        var headers = includeTimes
            ? new[] { Text(request, "Day", "Día"), Text(request, "Time", "Hora"), Text(request, "Scene / Slugline", "Escena / Encabezado"), Text(request, "Pages", "Páginas"), Text(request, "Duration", "Duración") }
            : new[] { Text(request, "Day", "Día"), Text(request, "Type", "Tipo"), Text(request, "Scene / Slugline", "Escena / Encabezado"), Text(request, "Cast", "Reparto"), Text(request, "Pages", "Páginas") };
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
                var headers = new List<string> { Text(request, "Cast", "Reparto") };
                headers.AddRange(dayChunk.Select(day => day.Date.ToString("d", ReportCulture(request)) + (day.IsBlackout ? "*" : string.Empty)));
                var rows = actorChunk.Select(entry =>
                {
                    var cells = new List<string> { entry.Key };
                    cells.AddRange(entry.Value.Skip(dayStart).Take(dayChunk.Count));
                    return cells.ToArray();
                }).ToList();
                RenderTable(document, $"{request.Project.ProjectTitle} — {Text(request, "Days Out of Days", "Días de reparto")}", headers.ToArray(), rows, landscape: true);
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
            var y = DrawHeader(graphics, page, $"{request.Project.ProjectTitle} — {Text(request, "Scene", "Escena")} {scene.SceneNumber}", scene.Title);
            var sections = new (string Label, IEnumerable<string> Values)[]
            {
                (Text(request, "Cast", "Reparto"), scene.Cast), ("Extras", scene.Extras), (Text(request, "Props", "Utilería"), scene.Props),
                (Text(request, "Set Dressing", "Ambientación"), scene.SetDressing), (Text(request, "Wardrobe", "Vestuario"), scene.Wardrobe),
                (Text(request, "Makeup / Hair", "Maquillaje / Peinado"), scene.MakeupHair), (Text(request, "Vehicles", "Vehículos"), scene.Vehicles),
                (Text(request, "Special Equipment", "Equipo especial"), scene.SpecialEquipment), (Text(request, "Stunts", "Dobles"), scene.Stunts),
                ("SFX", scene.Sfx), ("VFX", scene.Vfx), (Text(request, "Notes", "Notas"), [scene.BreakdownNotes])
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
        var resolvedCast = sheet.CastCallEntries.Select(entry =>
        {
            var member = production.CastList.FirstOrDefault(candidate => string.Equals(
                candidate.CharacterName.Trim(), entry.CharacterName.Trim(), StringComparison.OrdinalIgnoreCase));
            return (Entry: entry, Actor: member?.ActorName ?? entry.ActorName, Character: member?.CharacterName ?? entry.CharacterName);
        }).ToList();
        var resolvedLocations = sheet.Locations.Select(location =>
            production.LocationRoster.FirstOrDefault(candidate => candidate.Id == location.Id) ?? location).ToList();
        var selectedCrewIds = sheet.CrewIDOverride?.ToHashSet() ?? [];
        var resolvedCrew = production.Crew.Where(member => selectedCrewIds.Contains(member.Id)).ToList();
        var page = CreatePage(document, landscape: false);
        using var graphics = XGraphics.FromPdfPage(page);
        var y = DrawHeader(graphics, page, request.Project.ProjectTitle,
            $"{Text(request, "Call Sheet", "Hoja de llamado")} — {day.Date.ToString("D", ReportCulture(request))}");
        var lines = new[]
        {
            $"{Text(request, "General Call", "Llamado general")}: {sheet.GeneralCallTime}",
            $"{Text(request, "Director", "Dirección")}: {production.DirectorName} {production.DirectorPhone}",
            $"{Text(request, "Producer", "Producción")}: {production.ProducerName} {production.ProducerPhone}",
            $"1st AD: {production.AdName} {production.AdPhone}",
            $"{Text(request, "Ready", "Listos")}: {sheet.ReadyToShootTime}  {Text(request, "Lunch", "Comida")}: {sheet.LunchTime}  Wrap: {sheet.WrapTime}",
            $"{Text(request, "Weather", "Clima")}: {sheet.WeatherTemp} {sheet.WeatherCondition} {sheet.WeatherPrecipWind}",
            $"Hospital: {sheet.NearestHospital}",
            $"{Text(request, "Locations", "Locaciones")}: {string.Join("; ", resolvedLocations.Select(location => $"{location.Name} — {location.Address}"))}",
            $"{Text(request, "Scenes", "Escenas")}: {string.Join(", ", day.Scenes.Where(IsNormal).Select(scene => scene.DisplayTitle))}",
            $"{Text(request, "Cast", "Reparto")}: {string.Join("; ", resolvedCast.Select(value => $"{value.Actor} / {value.Character} / {value.Entry.OnSetTime}"))}",
            $"{Text(request, "Crew", "Equipo")}: {string.Join("; ", resolvedCrew.Select(member => $"{member.Role}: {member.Name}").Concat(sheet.CrewCallEntries.Select(entry => $"{entry.Role}: {entry.Name} {entry.CallTime}")))}",
            $"{Text(request, "Notes", "Notas")}: {string.Join("; ", sheet.ProductionNotes.Append(sheet.Notes).Where(value => !string.IsNullOrWhiteSpace(value)))}"
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

    private static CultureInfo ReportCulture(ReportRequest request) => request.Language == ReportLanguage.Spanish
        ? CultureInfo.GetCultureInfo("es-ES")
        : CultureInfo.GetCultureInfo("en-US");

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
