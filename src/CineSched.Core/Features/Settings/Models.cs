namespace CineSched.Core.Features.Settings;

public enum AppLanguage
{
    English,
    Spanish
}

public enum AppTheme
{
    System,
    Blue,
    Green,
    Yellow
}

public enum ColorMode
{
    System,
    Light,
    Dark
}

public sealed record AppSettings(
    AppLanguage Language = AppLanguage.English,
    AppTheme Theme = AppTheme.Blue,
    ColorMode ColorMode = ColorMode.System,
    bool IsSidebarCollapsed = false,
    bool IncludeHoldDays = true);
