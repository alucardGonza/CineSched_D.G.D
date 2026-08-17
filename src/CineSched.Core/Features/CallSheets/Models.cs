namespace CineSched.Core.Features.CallSheets;

public sealed class CastCallEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CharacterName { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string SceneNumbers { get; set; } = string.Empty;
    public string Ecdt { get; set; } = "E";
    public string PickupTime { get; set; } = string.Empty;
    public string HmuWardrobeTime { get; set; } = string.Empty;
    public string OnSetTime { get; set; } = string.Empty;
    public string WrapTime { get; set; } = string.Empty;
    public string LocationIndex { get; set; } = "1";
}

public sealed class CrewCallEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CallTime { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public sealed class CallSheetData
{
    public string GeneralCallTime { get; set; } = string.Empty;
    public string WorkDaySchedule { get; set; } = string.Empty;
    public string ReadyToShootTime { get; set; } = string.Empty;
    public string LunchTime { get; set; } = string.Empty;
    public string SnackTime { get; set; } = string.Empty;
    public string DinnerTime { get; set; } = string.Empty;
    public string WrapTime { get; set; } = string.Empty;
    public string QuoteOfTheDay { get; set; } = string.Empty;
    public string ProdManagerContact { get; set; } = string.Empty;
    public string AdContact { get; set; } = string.Empty;
    public string WeatherTemp { get; set; } = string.Empty;
    public string WeatherCondition { get; set; } = string.Empty;
    public string WeatherPrecipWind { get; set; } = string.Empty;
    public string SunTimes { get; set; } = string.Empty;
    public string BasecampLocation { get; set; } = string.Empty;
    public string NearestHospital { get; set; } = string.Empty;
    public List<CastCallEntry> CastCallEntries { get; set; } = [];
    public List<CrewCallEntry> CrewCallEntries { get; set; } = [];
    public List<string> ProductionNotes { get; set; } = [];
    public List<CineSched.Core.Features.Production.Location> Locations { get; set; } = [];
    public List<string>? CastOverride { get; set; }
    public List<string>? CrewOverride { get; set; }
    public List<Guid>? CrewIDOverride { get; set; }
    public List<string>? CrewOneOffs { get; set; }
    public string Notes { get; set; } = string.Empty;

    public bool HasData =>
        new[]
        {
            GeneralCallTime, WorkDaySchedule, ReadyToShootTime, LunchTime, SnackTime, DinnerTime,
            WrapTime, QuoteOfTheDay, ProdManagerContact, AdContact, WeatherTemp, WeatherCondition,
            WeatherPrecipWind, SunTimes, BasecampLocation, NearestHospital, Notes
        }.Any(value => !string.IsNullOrWhiteSpace(value)) ||
        Locations.Count > 0 || CastCallEntries.Count > 0 || CrewCallEntries.Count > 0 ||
        ProductionNotes.Any(value => !string.IsNullOrWhiteSpace(value)) ||
        CastOverride?.Count > 0 || CrewOverride?.Count > 0 || CrewIDOverride?.Count > 0 || CrewOneOffs?.Count > 0;
}
