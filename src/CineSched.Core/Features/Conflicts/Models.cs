namespace CineSched.Core.Features.Conflicts;

public sealed record ScheduleConflict(
    Guid SceneId,
    Guid ShootDayId,
    DateTimeOffset Date,
    string CharacterName,
    string ActorName,
    bool IsBlackout);
