namespace CineSched.Core.Features.Conflicts;

public sealed record ScheduleConflict(
    Guid SceneId,
    Guid ShootDayId,
    DateTimeOffset Date,
    string CharacterName,
    string ActorName,
    bool IsBlackout)
{
    public string DisplayString => IsBlackout
        ? $"{Date:yyyy-MM-dd}: blackout day"
        : $"{Date:yyyy-MM-dd}: {ActorName} — {CharacterName}";
}
