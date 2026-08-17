using System.Globalization;
using CineSched.Core.Common;
using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.Stripboard;

public sealed class StripboardService(ProjectService projects)
{
    public Result<TimelineResult> CalculateTimeline(Guid dayId)
    {
        var day = projects.Snapshot().ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null)
        {
            return Result<TimelineResult>.Failure("schedule.invalid-target", "The shoot day does not exist.");
        }

        var generalCall = ParseTime(day.CallSheet.GeneralCallTime, day.Date) ?? day.Date.AddHours(7);
        var cursor = generalCall;
        var items = new List<TimelineItem>(day.Scenes.Count);

        foreach (var scene in day.Scenes)
        {
            var fixedStart = ParseTime(scene.CustomStartTime, day.Date);
            var start = fixedStart is null || fixedStart < cursor ? cursor : fixedStart.Value;
            var duration = Math.Max(0, scene.EstimatedTime);
            var end = start.AddMinutes(duration);
            items.Add(new TimelineItem(scene.Id, start, end, fixedStart is not null));
            cursor = end;
        }

        return Result<TimelineResult>.Success(new TimelineResult(items, items.Count == 0 ? null : cursor));
    }

    public Result<Scene> AddBanner(
        Guid dayId,
        BannerType type,
        string title,
        string note,
        int durationMinutes,
        string colorHex,
        string? startTime = null)
    {
        if (string.IsNullOrWhiteSpace(title) || durationMinutes < 0)
        {
            return Result<Scene>.Failure("scene.validation-failed", "A banner needs a title and non-negative duration.");
        }

        var banner = new Scene
        {
            Title = title.Trim(),
            BannerTitle = title.Trim(),
            BannerNote = note.Trim(),
            BannerColorHex = colorHex.Trim(),
            BannerType = type,
            IsBanner = true,
            EstimatedTime = durationMinutes,
            CustomStartTime = startTime?.Trim() ?? string.Empty
        };

        return AddItem(dayId, banner, "stripboard.banner-added");
    }

    public Result<Scene> AddMeal(Guid dayId, MealKind mealKind, string startTime, string? colorHex = null)
    {
        var duration = mealKind == MealKind.Snack ? 15 : 60;
        var meal = new Scene
        {
            Title = mealKind.ToString(),
            BannerTitle = mealKind.ToString(),
            BannerType = BannerType.MealBreak,
            MealKind = mealKind,
            IsBanner = true,
            IsAutoMeal = true,
            EstimatedTime = duration,
            CustomStartTime = startTime.Trim(),
            BannerColorHex = colorHex ?? "#D97706"
        };

        return AddItem(dayId, meal, "stripboard.meal-added");
    }

    public Result<Scene> AddCalendarEvent(
        Guid dayId,
        string title,
        string startTime,
        string colorHex,
        int durationMinutes = 0)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<Scene>.Failure("scene.validation-failed", "A calendar event needs a title.");
        }

        var calendarEvent = new Scene
        {
            Title = title.Trim(),
            IsCalendarEvent = true,
            CustomStartTime = startTime.Trim(),
            BannerColorHex = colorHex.Trim(),
            EstimatedTime = Math.Max(0, durationMinutes)
        };

        return AddItem(dayId, calendarEvent, "stripboard.calendar-event-added");
    }

    private Result<Scene> AddItem(Guid dayId, Scene item, string changeKind)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null)
        {
            return Result<Scene>.Failure("schedule.invalid-target", "The shoot day does not exist.");
        }

        day.Scenes.Add(item);
        projects.Apply(document, changeKind);
        return Result<Scene>.Success(item);
    }

    private static DateTimeOffset? ParseTime(string? text, DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var formats = new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" };
        if (!DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var time))
        {
            return null;
        }

        return new DateTimeOffset(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, date.Offset);
    }
}
