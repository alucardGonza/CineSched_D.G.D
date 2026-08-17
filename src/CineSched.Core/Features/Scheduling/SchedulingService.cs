using CineSched.Core.Common;
using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.Scheduling;

public sealed class SchedulingService(ProjectService projects)
{
    public bool CanUndo => projects.CanUndo;

    public bool CanRedo => projects.CanRedo;

    public IReadOnlyList<IReadOnlyList<CalendarSlot>> GetCalendarWeeks()
    {
        var days = projects.Snapshot().ShootDays.OrderBy(day => day.Date).ToList();
        if (days.Count == 0) return [];
        var first = DateOnly.FromDateTime(days[0].Date.Date);
        var last = DateOnly.FromDateTime(days[^1].Date.Date);
        var offsetFromMonday = ((int)first.DayOfWeek + 6) % 7;
        var calendarStart = first.AddDays(-offsetFromMonday);
        var offsetToSunday = 6 - (((int)last.DayOfWeek + 6) % 7);
        var calendarEnd = last.AddDays(offsetToSunday);
        var byDate = days.ToDictionary(day => DateOnly.FromDateTime(day.Date.Date));
        var weeks = new List<IReadOnlyList<CalendarSlot>>();
        for (var weekStart = calendarStart; weekStart <= calendarEnd; weekStart = weekStart.AddDays(7))
        {
            var week = new List<CalendarSlot>(7);
            for (var index = 0; index < 7; index++)
            {
                var date = weekStart.AddDays(index);
                byDate.TryGetValue(date, out var shootDay);
                week.Add(new CalendarSlot(date, shootDay, date >= first && date <= last));
            }

            weeks.Add(week);
        }

        return weeks;
    }

    public Result<ProjectDocument> ChangeDateRange(ChangeDateRangeRequest request)
    {
        var start = StartOfDay(request.Start);
        var end = StartOfDay(request.End);
        if (end < start)
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-range", "The end date must be on or after the start date.");
        }

        var document = projects.Snapshot();
        var sourceDays = document.ShootDays;
        if (request.ShiftExisting && sourceDays.Count > 0)
        {
            var currentStart = sourceDays.Min(day => day.Date);
            var delta = start.Date - currentStart.Date;
            foreach (var day in sourceDays)
            {
                day.Date = day.Date.Add(delta);
            }
        }

        var byDate = sourceDays.ToDictionary(day => DateOnly.FromDateTime(day.Date.LocalDateTime));
        var days = new List<ShootDay>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var key = DateOnly.FromDateTime(date.LocalDateTime);
            if (byDate.TryGetValue(key, out var existing))
            {
                existing.Date = date;
                days.Add(existing);
            }
            else
            {
                days.Add(new ShootDay { Date = date });
            }
        }

        var retainedIds = days.Select(day => day.Id).ToHashSet();
        foreach (var removedDay in sourceDays.Where(day => !retainedIds.Contains(day.Id)))
        {
            document.AllScenes.AddRange(removedDay.Scenes.Where(scene => !scene.IsBanner && !scene.IsCalendarEvent));
        }

        document.ShootDays = days;
        document.IsShiftModeEnabled = request.ShiftExisting;
        return Result<ProjectDocument>.Success(projects.Apply(document, "schedule.range-changed"));
    }

    public Result<ProjectDocument> MoveScenes(MoveScenesRequest request)
    {
        if (request.SceneIds.Count == 0)
        {
            return Result<ProjectDocument>.Failure("schedule.no-scenes", "Select at least one scene to move.");
        }

        var document = projects.Snapshot();
        var target = document.ShootDays.FirstOrDefault(day => day.Id == request.TargetDayId);
        if (target is null)
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-target", "The target shoot day does not exist.");
        }

        var selected = CollectInDocumentOrder(document, request.SceneIds);
        if (selected.Count != request.SceneIds.Distinct().Count())
        {
            return Result<ProjectDocument>.Failure("scene.not-found", "One or more selected scenes no longer exist.");
        }

        RemoveEverywhere(document, request.SceneIds);
        var index = Math.Clamp(request.TargetIndex, 0, target.Scenes.Count);
        target.Scenes.InsertRange(index, selected);
        return Result<ProjectDocument>.Success(projects.Apply(document, "schedule.scenes-moved"));
    }

    public Result<ProjectDocument> ReorderScenes(ReorderScenesRequest request) =>
        MoveScenes(new MoveScenesRequest(request.SceneIds, request.DayId, request.DayId, request.TargetIndex));

    public Result<ProjectDocument> RemoveFromDay(Guid dayId, IReadOnlyList<Guid> sceneIds)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(value => value.Id == dayId);
        if (day is null)
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-source", "The source shoot day does not exist.");
        }

        var selected = day.Scenes.Where(scene => sceneIds.Contains(scene.Id)).ToList();
        if (selected.Count != sceneIds.Distinct().Count())
        {
            return Result<ProjectDocument>.Failure("scene.not-found", "One or more selected scenes no longer exist.");
        }

        if (selected.Any(scene => scene.IsBanner || scene.IsCalendarEvent))
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-remove", "Banners and calendar events cannot be sent to the Boneyard.");
        }

        day.Scenes.RemoveAll(scene => sceneIds.Contains(scene.Id));
        document.AllScenes.AddRange(selected);
        return Result<ProjectDocument>.Success(projects.Apply(document, "schedule.scenes-removed"));
    }

    public Result<ProjectDocument> MoveWholeDay(MoveWholeDayRequest request)
    {
        var document = projects.Snapshot();
        var source = document.ShootDays.FirstOrDefault(day => day.Id == request.SourceDayId);
        var target = document.ShootDays.FirstOrDefault(day => day.Id == request.TargetDayId);
        if (source is null || target is null)
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-target", "The source or target shoot day does not exist.");
        }

        (source.Scenes, target.Scenes) = (target.Scenes, source.Scenes);
        (source.CallSheet, target.CallSheet) = (target.CallSheet, source.CallSheet);
        return Result<ProjectDocument>.Success(projects.Apply(document, "schedule.day-moved"));
    }

    public Result<ProjectDocument> SetBlackout(SetBlackoutRequest request)
    {
        var document = projects.Snapshot();
        var day = document.ShootDays.FirstOrDefault(value => value.Id == request.DayId);
        if (day is null)
        {
            return Result<ProjectDocument>.Failure("schedule.invalid-target", "The shoot day does not exist.");
        }

        if (request.ApplyToMatchingWeekday)
        {
            foreach (var matching in document.ShootDays.Where(value => value.Date.DayOfWeek == day.Date.DayOfWeek))
            {
                matching.IsBlackout = request.IsBlackout;
            }
        }
        else
        {
            day.IsBlackout = request.IsBlackout;
        }

        return Result<ProjectDocument>.Success(projects.Apply(document, "schedule.blackout-changed"));
    }

    public Result<ProjectDocument> Undo() => projects.Undo();

    public Result<ProjectDocument> Redo() => projects.Redo();

    private static List<Scene> CollectInDocumentOrder(ProjectDocument document, IReadOnlyList<Guid> sceneIds)
    {
        var ids = sceneIds.ToHashSet();
        return document.AllScenes
            .Concat(document.ShootDays.SelectMany(day => day.Scenes))
            .Where(scene => ids.Contains(scene.Id))
            .ToList();
    }

    private static void RemoveEverywhere(ProjectDocument document, IReadOnlyList<Guid> sceneIds)
    {
        var ids = sceneIds.ToHashSet();
        document.AllScenes.RemoveAll(scene => ids.Contains(scene.Id));
        foreach (var day in document.ShootDays)
        {
            day.Scenes.RemoveAll(scene => ids.Contains(scene.Id));
        }
    }

    private static DateTimeOffset StartOfDay(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);
}
