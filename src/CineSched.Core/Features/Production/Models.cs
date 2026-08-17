using CineSched.Core.Features.ScheduleLock;

namespace CineSched.Core.Features.Production;

public sealed class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public sealed class CrewMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsDailyDefault { get; set; }

    public string DisplayString => string.IsNullOrEmpty(Role) ? Name : $"{Name} — {Role}";
}

public sealed class DateRange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public bool Contains(DateTimeOffset date) =>
        DateOnly.FromDateTime(date.LocalDateTime) >= DateOnly.FromDateTime(Start.LocalDateTime) &&
        DateOnly.FromDateTime(date.LocalDateTime) <= DateOnly.FromDateTime(End.LocalDateTime);

    public string DisplayString => $"{Start:yyyy-MM-dd} – {End:yyyy-MM-dd}";
}

public sealed class CastMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorName { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public List<DateRange> UnavailableRanges { get; set; } = [];

    public string DisplayString => string.IsNullOrEmpty(ActorName)
        ? CharacterName
        : $"{ActorName} — {CharacterName}";

    public string UnavailableSummary => UnavailableRanges.Count == 0
        ? "Available"
        : string.Join(", ", UnavailableRanges.OrderBy(range => range.Start)
            .Select(range => $"{range.Start:yyyy-MM-dd}–{range.End:yyyy-MM-dd}"));
}

public sealed class ProductionInfo
{
    public string CompanyName { get; set; } = string.Empty;
    public string DirectorName { get; set; } = string.Empty;
    public string DirectorPhone { get; set; } = string.Empty;
    public string ProducerName { get; set; } = string.Empty;
    public string ProducerPhone { get; set; } = string.Empty;
    public string AdName { get; set; } = string.Empty;
    public string AdPhone { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string DefaultLunchTime { get; set; } = "01:30 PM";
    public List<CrewMember> Crew { get; set; } = [];
    public List<CastMember> CastList { get; set; } = [];
    public List<Location> LocationRoster { get; set; } = [];
    public ScheduleLockData? ScheduleLock { get; set; }
}
