using System.Text.Json.Serialization;

namespace CineSched.Core.Features.Stripboard;

[JsonConverter(typeof(JsonStringEnumConverter<BannerType>))]
public enum BannerType
{
    [JsonStringEnumMemberName("Company Move")]
    CompanyMove,
    [JsonStringEnumMemberName("Meal Break")]
    MealBreak,
    [JsonStringEnumMemberName("Notice / Note")]
    Notice,
    [JsonStringEnumMemberName("Custom Banner")]
    Custom
}

[JsonConverter(typeof(JsonStringEnumConverter<MealKind>))]
public enum MealKind
{
    [JsonStringEnumMemberName("Lunch")]
    Lunch,
    [JsonStringEnumMemberName("Snack")]
    Snack,
    [JsonStringEnumMemberName("Dinner")]
    Dinner,
    [JsonStringEnumMemberName("Wrap")]
    Wrap
}

public sealed record TimelineItem(
    Guid SceneId,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsFixedAnchor);

public sealed record TimelineResult(
    IReadOnlyList<TimelineItem> Items,
    DateTimeOffset? WrapTime);
