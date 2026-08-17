namespace CineSched.Tests.Unit;

public sealed class CallSheetServiceTests
{
    [Fact]
    public void HasData_DetectsAnyRelevantField()
    {
        Assert.True(new CallSheetData { WeatherCondition = "Rain" }.HasData);
        Assert.True(new CallSheetData { CrewCallEntries = [new CrewCallEntry()] }.HasData);
        Assert.False(new CallSheetData().HasData);
    }

    [Fact]
    public void Initialize_ResolvesUniqueCastLocationsAndDailyCrew()
    {
        var projects = TestData.Project();
        var cast = new CastMember { CharacterName = "ANA", ActorName = "Actor" };
        var daily = new CrewMember { Name = "Grip", Role = "Key Grip", IsDailyDefault = true };
        projects.Update(document =>
        {
            document.ProductionInfo!.CastList.Add(cast);
            document.ProductionInfo.Crew.Add(daily);
            var first = TestData.Scene("1", cast: "ANA");
            first.RealLocation = "Studio";
            first.LocationAddress = "1 Main";
            var second = TestData.Scene("2", cast: "ANA");
            second.RealLocation = " studio ";
            second.LocationAddress = " 1 main ";
            document.ShootDays[0].Scenes.AddRange([first, second]);
        });
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;
        var service = new CallSheetService(projects);

        var sheet = service.Initialize(dayId);

        Assert.Single(sheet.Value!.CastCallEntries);
        Assert.Equal("Actor", sheet.Value.CastCallEntries[0].ActorName);
        Assert.Single(sheet.Value.Locations);
        Assert.Contains(daily.Id, sheet.Value.CrewIDOverride!);
    }

    [Fact]
    public void SelectedCrew_ReflectsRosterEditsThroughStableId()
    {
        var projects = TestData.Project();
        var crew = new CrewMember { Name = "Before", Role = "Grip", IsDailyDefault = true };
        projects.Update(document => document.ProductionInfo!.Crew.Add(crew));
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;
        var sheets = new CallSheetService(projects);
        sheets.Initialize(dayId);

        new ProductionService(projects).UpdateCrew(crew.Id, "After", "Gaffer", "", true);

        Assert.Equal("After", sheets.GetSelectedCrew(dayId).Single().Name);
    }

    [Fact]
    public void ResolvedCastAndLocations_ReflectProductionRosterEdits()
    {
        var projects = TestData.Project();
        var cast = new CastMember { CharacterName = "ANA", ActorName = "Before" };
        var location = new Location { Name = "Old Stage", Address = "Old Address" };
        projects.Update(document =>
        {
            document.ProductionInfo!.CastList.Add(cast);
            document.ProductionInfo.LocationRoster.Add(location);
            document.ShootDays[0].Scenes.Add(TestData.Scene("1", cast: "ANA"));
            document.ShootDays[0].Scenes[0].RealLocation = location.Name;
            document.ShootDays[0].Scenes[0].LocationAddress = location.Address;
        });
        var dayId = projects.GetSnapshot().Document.ShootDays[0].Id;
        var sheets = new CallSheetService(projects);
        sheets.Initialize(dayId);
        var production = new ProductionService(projects);

        production.UpdateCast(cast.Id, "After", "ANA");
        production.UpdateLocation(location.Id, "New Stage", "New Address");

        Assert.Equal("After", sheets.GetResolvedCastEntries(dayId).Single().ActorName);
        Assert.Equal("New Stage", sheets.GetResolvedLocations(dayId).Single().Name);
    }
}
