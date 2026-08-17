using System.Text.Json.Serialization;
using CineSched.Core.Features.Stripboard;

namespace CineSched.Core.Features.Scenes;

[JsonConverter(typeof(JsonStringEnumConverter<DayNightType>))]
public enum DayNightType
{
    [JsonStringEnumMemberName("DAY")]
    Day,
    [JsonStringEnumMemberName("NIGHT")]
    Night,
    [JsonStringEnumMemberName("DAWN")]
    Dawn,
    [JsonStringEnumMemberName("DUSK")]
    Dusk,
    [JsonStringEnumMemberName("AFTERNOON")]
    Afternoon,
    [JsonStringEnumMemberName("CUSTOM")]
    Custom
}

public enum BoneyardSort
{
    Default,
    Location,
    InteriorExterior,
    Cast,
    DayNight
}

public sealed class Scene
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string SceneNumber { get; set; } = string.Empty;
    public int Duration { get; set; }
    public int EstimatedTime { get; set; }
    public DayNightType DayNightType { get; set; } = DayNightType.Day;
    public List<string> Cast { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string RealLocation { get; set; } = string.Empty;
    public string LocationAddress { get; set; } = string.Empty;
    public List<string> Extras { get; set; } = [];
    public List<string> Props { get; set; } = [];
    public List<string> SetDressing { get; set; } = [];
    public List<string> Wardrobe { get; set; } = [];
    public List<string> MakeupHair { get; set; } = [];
    public List<string> Vehicles { get; set; } = [];
    public List<string> SpecialEquipment { get; set; } = [];
    public List<string> Stunts { get; set; } = [];
    public List<string> Sfx { get; set; } = [];
    public List<string> Vfx { get; set; } = [];
    public string BreakdownNotes { get; set; } = string.Empty;
    public bool IsBanner { get; set; }
    public BannerType? BannerType { get; set; }
    public string BannerTitle { get; set; } = string.Empty;
    public string BannerNote { get; set; } = string.Empty;
    public string BannerColorHex { get; set; } = string.Empty;
    public bool IsAutoMeal { get; set; }
    public MealKind? MealKind { get; set; }
    public bool IsCalendarEvent { get; set; }
    public string CustomStartTime { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayTitle => IsBanner || string.IsNullOrWhiteSpace(SceneNumber)
        ? Title
        : $"{SceneNumber.Trim()}. {Title}";
}

public sealed record SceneInput(
    string Title,
    string SceneNumber = "",
    string Duration = "0",
    string EstimatedTime = "0",
    DayNightType DayNightType = DayNightType.Day,
    IReadOnlyList<string>? Cast = null,
    string Summary = "",
    string RealLocation = "",
    string LocationAddress = "",
    IReadOnlyList<string>? Extras = null,
    IReadOnlyList<string>? Props = null,
    IReadOnlyList<string>? SetDressing = null,
    IReadOnlyList<string>? Wardrobe = null,
    IReadOnlyList<string>? MakeupHair = null,
    IReadOnlyList<string>? Vehicles = null,
    IReadOnlyList<string>? SpecialEquipment = null,
    IReadOnlyList<string>? Stunts = null,
    IReadOnlyList<string>? Sfx = null,
    IReadOnlyList<string>? Vfx = null,
    string BreakdownNotes = "");

public sealed record SceneSearchResult(Scene Scene, DateTimeOffset? DayDate);
