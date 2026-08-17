using CineSched.Core.Features.Settings;

namespace CineSched.Tests.Unit;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Localization_UsesSelectedLanguageAndVisibleFallback()
    {
        var service = new SettingsService();
        service.Update(new AppSettings(Language: AppLanguage.Spanish));

        Assert.Equal("Calendario", service.GetText("nav.calendar"));
        Assert.Equal("[missing.key]", service.GetText("missing.key"));
    }

    [Theory]
    [InlineData(AppLanguage.English, "en-US", "August")]
    [InlineData(AppLanguage.Spanish, "es-ES", "agosto")]
    public void Language_ControlsResourcesAndDateCulture(AppLanguage language, string culture, string month)
    {
        var service = new SettingsService();
        service.Update(new AppSettings(Language: language));

        Assert.Equal(culture, service.Culture.Name);
        Assert.Contains(month, service.FormatDate(new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero)), StringComparison.OrdinalIgnoreCase);
    }
}
