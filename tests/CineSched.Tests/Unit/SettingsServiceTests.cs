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
}
