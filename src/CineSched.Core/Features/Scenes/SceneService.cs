using System.Globalization;
using System.Text.RegularExpressions;
using CineSched.Core.Common;
using CineSched.Core.Features.Projects;

namespace CineSched.Core.Features.Scenes;

public sealed partial class SceneService(ProjectService projects)
{
    public Result<Scene> CreateScene(SceneInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Result<Scene>.Failure("scene.validation-failed", "A scene title is required.");
        }

        var duration = ParseDuration(input.Duration, input.DayNightType == DayNightType.Custom);
        if (!duration.IsSuccess)
        {
            return Result<Scene>.Failure(duration.Error!.Code, duration.Error.Message);
        }

        var estimated = ParseEstimatedTime(input.EstimatedTime, input.DayNightType == DayNightType.Custom);
        if (!estimated.IsSuccess)
        {
            return Result<Scene>.Failure(estimated.Error!.Code, estimated.Error.Message);
        }

        var scene = new Scene
        {
            Title = input.Title.Trim(),
            SceneNumber = input.SceneNumber.Trim(),
            Duration = duration.Value,
            EstimatedTime = estimated.Value,
            DayNightType = input.DayNightType,
            Cast = Normalize(input.Cast),
            Summary = input.Summary.Trim(),
            RealLocation = input.RealLocation.Trim(),
            LocationAddress = input.LocationAddress.Trim(),
            Extras = Normalize(input.Extras),
            Props = Normalize(input.Props),
            SetDressing = Normalize(input.SetDressing),
            Wardrobe = Normalize(input.Wardrobe),
            MakeupHair = Normalize(input.MakeupHair),
            Vehicles = Normalize(input.Vehicles),
            SpecialEquipment = Normalize(input.SpecialEquipment),
            Stunts = Normalize(input.Stunts),
            Sfx = Normalize(input.Sfx),
            Vfx = Normalize(input.Vfx),
            BreakdownNotes = input.BreakdownNotes.Trim()
        };
        AutoExtractSceneNumber(scene);

        var document = projects.Snapshot();
        document.AllScenes.Add(scene);
        projects.Apply(document, "scene.created");
        return Result<Scene>.Success(scene);
    }

    public Result<Scene> EditScene(Scene updated)
    {
        if (string.IsNullOrWhiteSpace(updated.Title))
        {
            return Result<Scene>.Failure("scene.validation-failed", "A scene title is required.");
        }

        var document = projects.Snapshot();
        if (!Replace(document.AllScenes, updated))
        {
            var replaced = false;
            foreach (var day in document.ShootDays)
            {
                if (Replace(day.Scenes, updated))
                {
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                return Result<Scene>.Failure("scene.not-found", "The scene no longer exists.");
            }
        }

        projects.Apply(document, "scene.edited", structural: false);
        return Result<Scene>.Success(updated);
    }

    public Result<Scene> EditScene(Guid sceneId, SceneInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return Result<Scene>.Failure("scene.validation-failed", "A scene title is required.");
        var duration = ParseDuration(input.Duration, input.DayNightType == DayNightType.Custom);
        if (!duration.IsSuccess)
            return Result<Scene>.Failure(duration.Error!.Code, duration.Error.Message, duration.Error.Details);
        var estimated = ParseEstimatedTime(input.EstimatedTime, input.DayNightType == DayNightType.Custom);
        if (!estimated.IsSuccess)
            return Result<Scene>.Failure(estimated.Error!.Code, estimated.Error.Message, estimated.Error.Details);

        var document = projects.Snapshot();
        var current = document.AllScenes.Concat(document.ShootDays.SelectMany(day => day.Scenes))
            .FirstOrDefault(scene => scene.Id == sceneId);
        if (current is null)
            return Result<Scene>.Failure("scene.not-found", "The scene no longer exists.");

        current.Title = input.Title.Trim();
        current.SceneNumber = input.SceneNumber.Trim();
        current.Duration = duration.Value;
        current.EstimatedTime = estimated.Value;
        current.DayNightType = input.DayNightType;
        current.Cast = Normalize(input.Cast);
        current.Summary = input.Summary.Trim();
        current.RealLocation = input.RealLocation.Trim();
        current.LocationAddress = input.LocationAddress.Trim();
        current.Extras = Normalize(input.Extras);
        current.Props = Normalize(input.Props);
        current.SetDressing = Normalize(input.SetDressing);
        current.Wardrobe = Normalize(input.Wardrobe);
        current.MakeupHair = Normalize(input.MakeupHair);
        current.Vehicles = Normalize(input.Vehicles);
        current.SpecialEquipment = Normalize(input.SpecialEquipment);
        current.Stunts = Normalize(input.Stunts);
        current.Sfx = Normalize(input.Sfx);
        current.Vfx = Normalize(input.Vfx);
        current.BreakdownNotes = input.BreakdownNotes.Trim();
        AutoExtractSceneNumber(current);
        projects.Apply(document, "scene.edited", structural: false);
        return Result<Scene>.Success(current);
    }

    public Result<Scene> DuplicateScene(Guid sceneId)
    {
        var document = projects.Snapshot();
        var boneyardIndex = document.AllScenes.FindIndex(scene => scene.Id == sceneId);
        if (boneyardIndex >= 0)
        {
            var duplicate = CloneScene(document.AllScenes[boneyardIndex]);
            document.AllScenes.Insert(boneyardIndex + 1, duplicate);
            projects.Apply(document, "scene.duplicated");
            return Result<Scene>.Success(duplicate);
        }

        foreach (var day in document.ShootDays)
        {
            var index = day.Scenes.FindIndex(scene => scene.Id == sceneId);
            if (index < 0)
            {
                continue;
            }

            var duplicate = CloneScene(day.Scenes[index]);
            day.Scenes.Insert(index + 1, duplicate);
            projects.Apply(document, "scene.duplicated");
            return Result<Scene>.Success(duplicate);
        }

        return Result<Scene>.Failure("scene.not-found", "The scene no longer exists.");
    }

    public Result<Unit> DeleteScene(Guid sceneId)
    {
        var document = projects.Snapshot();
        var removed = document.AllScenes.RemoveAll(scene => scene.Id == sceneId) > 0;
        foreach (var day in document.ShootDays)
        {
            removed |= day.Scenes.RemoveAll(scene => scene.Id == sceneId) > 0;
        }

        if (!removed)
        {
            return Result<Unit>.Failure("scene.not-found", "The scene no longer exists.");
        }

        projects.Apply(document, "scene.deleted");
        return Result<Unit>.Success(Unit.Value);
    }

    public IReadOnlyList<SceneSearchResult> SearchScenes(string query)
    {
        var term = query.Trim();
        if (term.Length == 0)
        {
            return [];
        }

        var document = projects.Snapshot();
        var results = new List<SceneSearchResult>();
        results.AddRange(document.AllScenes.Where(Matches).Select(scene => new SceneSearchResult(scene, null)));
        foreach (var day in document.ShootDays)
        {
            results.AddRange(day.Scenes.Where(scene => !scene.IsCalendarEvent && Matches(scene)).Select(scene => new SceneSearchResult(scene, day.Date)));
        }

        return results;

        bool Matches(Scene scene) =>
            scene.DisplayTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            scene.Summary.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            scene.Cast.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<Scene> SortBoneyard(BoneyardSort sort)
    {
        var source = projects.Snapshot().AllScenes;
        return sort switch
        {
            BoneyardSort.Location => source.OrderBy(GetLocationSortKey, StringComparer.OrdinalIgnoreCase).ThenBy(GetScriptOrderKey).ToList(),
            BoneyardSort.InteriorExterior => source.OrderBy(scene => GetInteriorExterior(scene.Title), StringComparer.OrdinalIgnoreCase).ThenBy(GetScriptOrderKey).ToList(),
            BoneyardSort.Cast => source.OrderBy(scene => scene.Cast.FirstOrDefault() ?? "~", StringComparer.OrdinalIgnoreCase).ThenBy(GetScriptOrderKey).ToList(),
            BoneyardSort.DayNight => source.OrderBy(scene => DayNightOrder(scene.DayNightType)).ThenBy(GetScriptOrderKey).ToList(),
            _ => source.ToList()
        };
    }

    public IReadOnlyList<Scene> GetScriptOrder() =>
        projects.Snapshot().AllScenes
            .Concat(projects.Snapshot().ShootDays.SelectMany(day => day.Scenes))
            .Where(scene => !scene.IsBanner && !scene.IsCalendarEvent)
            .GroupBy(scene => scene.Id)
            .Select(group => group.First())
            .OrderBy(GetScriptOrderKey)
            .ToList();

    public Result<int> ParseDuration(string raw, bool optional = false)
    {
        var value = raw.Trim();
        if (value.Length == 0 && optional)
        {
            return Result<int>.Success(0);
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var eighths) && eighths >= 0)
        {
            return Result<int>.Success(eighths);
        }

        var mixed = MixedFractionRegex().Match(value);
        if (mixed.Success && TryFraction(mixed.Groups[2].Value, mixed.Groups[3].Value, out var fraction))
        {
            var whole = int.Parse(mixed.Groups[1].Value, CultureInfo.InvariantCulture);
            return Result<int>.Success((whole * 8) + fraction);
        }

        var simple = FractionRegex().Match(value);
        if (simple.Success && TryFraction(simple.Groups[1].Value, simple.Groups[2].Value, out fraction))
        {
            return Result<int>.Success(fraction);
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var pages) && pages >= 0)
        {
            return Result<int>.Success((int)Math.Round(pages * 8m, MidpointRounding.AwayFromZero));
        }

        return Result<int>.Failure("scene.validation-failed", $"'{raw}' is not a valid page duration.");
    }

    public Result<int> ParseEstimatedTime(string raw, bool optional = false)
    {
        var value = raw.Trim();
        if (value.Length == 0 && optional)
        {
            return Result<int>.Success(0);
        }

        var parts = value.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var hours) && hours >= 0 &&
            int.TryParse(parts[1], out var minutes) && minutes is >= 0 and < 60)
        {
            return Result<int>.Success((hours * 60) + minutes);
        }

        if (int.TryParse(value, out var number) && number >= 0)
        {
            return Result<int>.Success(number <= 10 ? number * 60 : number);
        }

        return Result<int>.Failure("scene.validation-failed", $"'{raw}' is not a valid estimated time.");
    }

    public void AutoExtractSceneNumber(Scene scene)
    {
        if (scene.IsBanner || !string.IsNullOrWhiteSpace(scene.SceneNumber))
        {
            return;
        }

        var match = LeadingSceneNumberRegex().Match(scene.Title.Trim());
        if (!match.Success)
        {
            return;
        }

        scene.SceneNumber = match.Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
        {
            scene.Title = match.Groups[2].Value.Trim();
        }
    }

    public static string GetDecorado(Scene scene)
    {
        if (!string.IsNullOrWhiteSpace(scene.RealLocation))
        {
            return scene.RealLocation.Trim().ToUpperInvariant();
        }

        var value = LeadingScenePrefixRegex().Replace(scene.Title.Trim(), string.Empty);
        value = InteriorExteriorPrefixRegex().Replace(value, string.Empty);
        value = TimeOfDaySuffixRegex().Replace(value, string.Empty).Trim(' ', '.', '-', '–', '—');
        return (value.Length == 0 ? scene.Title : value).ToUpperInvariant();
    }

    private static bool Replace(List<Scene> scenes, Scene updated)
    {
        var index = scenes.FindIndex(scene => scene.Id == updated.Id);
        if (index < 0)
        {
            return false;
        }

        scenes[index] = CloneScene(updated, keepId: true);
        return true;
    }

    private static Scene CloneScene(Scene source, bool keepId = false) => new()
    {
        Id = keepId ? source.Id : Guid.NewGuid(),
        Title = source.Title,
        SceneNumber = source.SceneNumber,
        Duration = source.Duration,
        EstimatedTime = source.EstimatedTime,
        DayNightType = source.DayNightType,
        Cast = [.. source.Cast],
        Summary = source.Summary,
        RealLocation = source.RealLocation,
        LocationAddress = source.LocationAddress,
        Extras = [.. source.Extras],
        Props = [.. source.Props],
        SetDressing = [.. source.SetDressing],
        Wardrobe = [.. source.Wardrobe],
        MakeupHair = [.. source.MakeupHair],
        Vehicles = [.. source.Vehicles],
        SpecialEquipment = [.. source.SpecialEquipment],
        Stunts = [.. source.Stunts],
        Sfx = [.. source.Sfx],
        Vfx = [.. source.Vfx],
        BreakdownNotes = source.BreakdownNotes,
        IsBanner = source.IsBanner,
        BannerType = source.BannerType,
        BannerTitle = source.BannerTitle,
        BannerNote = source.BannerNote,
        BannerColorHex = source.BannerColorHex,
        IsAutoMeal = source.IsAutoMeal,
        MealKind = source.MealKind,
        IsCalendarEvent = source.IsCalendarEvent,
        CustomStartTime = source.CustomStartTime
    };

    private static bool TryFraction(string numeratorRaw, string denominatorRaw, out int eighths)
    {
        eighths = 0;
        if (!int.TryParse(numeratorRaw, out var numerator) ||
            !int.TryParse(denominatorRaw, out var denominator) ||
            denominator <= 0 || numerator < 0)
        {
            return false;
        }

        eighths = (int)Math.Round((decimal)numerator / denominator * 8m, MidpointRounding.AwayFromZero);
        return true;
    }

    private static List<string> Normalize(IEnumerable<string>? values) => values?
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList() ?? [];

    private static string GetLocationSortKey(Scene scene) => GetDecorado(scene);

    private static string GetInteriorExterior(string title)
    {
        var value = LeadingScenePrefixRegex().Replace(title.Trim(), string.Empty).ToUpperInvariant();
        return value.StartsWith("EXT", StringComparison.Ordinal) ? "EXT" : "INT";
    }

    private static int DayNightOrder(DayNightType value) => value switch
    {
        DayNightType.Day => 0,
        DayNightType.Dawn => 1,
        DayNightType.Afternoon => 2,
        DayNightType.Dusk => 3,
        DayNightType.Night => 4,
        _ => 5
    };

    private static (int Number, string Letter, string Title) GetScriptOrderKey(Scene scene)
    {
        var value = string.IsNullOrWhiteSpace(scene.SceneNumber) ? scene.Title.Trim() : scene.SceneNumber.Trim();
        var match = SceneOrderRegex().Match(value);
        return match.Success
            ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), match.Groups[2].Value.ToUpperInvariant(), scene.Title)
            : (int.MaxValue, string.Empty, scene.Title);
    }

    [GeneratedRegex(@"^(\d+)\s+(\d+)\s*/\s*(\d+)$")]
    private static partial Regex MixedFractionRegex();

    [GeneratedRegex(@"^(\d+)\s*/\s*(\d+)$")]
    private static partial Regex FractionRegex();

    [GeneratedRegex(@"^#?(\d+[A-Za-z]?)\.?\s*[-–—.]?\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingSceneNumberRegex();

    [GeneratedRegex(@"^#?\d+[A-Za-z]?\.?\s*[-–—.]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingScenePrefixRegex();

    [GeneratedRegex(@"^(INT\.?/EXT\.?|INT\.?|EXT\.?|I/E\.?|INT\s+/\s+EXT)\s*[-–—.]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex InteriorExteriorPrefixRegex();

    [GeneratedRegex(@"\s*[-–—.]+\s*(DAY|NIGHT|DAWN|DUSK|AFTERNOON|DIA|NOCHE|TARDE|ATARDECER|AMANECER|CONTINUOUS|SAME)\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TimeOfDaySuffixRegex();

    [GeneratedRegex(@"^#?(\d+)([A-Za-z]?)")]
    private static partial Regex SceneOrderRegex();
}
