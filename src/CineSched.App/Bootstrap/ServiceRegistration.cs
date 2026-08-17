using CineSched.App.Services;
using CineSched.App.Shell;
using CineSched.Core.Features.CallSheets;
using CineSched.Core.Features.Conflicts;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Reports;
using CineSched.Core.Features.ScheduleLock;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.ScriptImport;
using CineSched.Core.Features.Settings;
using CineSched.Core.Features.Stripboard;

namespace CineSched.App;

internal static class ServiceRegistration
{
    public static IServiceCollection AddCineSched(IServiceCollection services)
    {
        services.AddSingleton<ProjectService>();
        services.AddSingleton<SceneService>();
        services.AddSingleton<SchedulingService>();
        services.AddSingleton<StripboardService>();
        services.AddSingleton<ProductionService>();
        services.AddSingleton<CallSheetService>();
        services.AddSingleton<ConflictService>();
        services.AddSingleton<ScheduleLockService>();
        services.AddSingleton<ScriptImportService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FileDialogService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<PreferencesService>();
        services.AddSingleton<RecentFilesService>();
        services.AddSingleton<AutosaveService>();
        services.AddSingleton<AppLifecycleService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainPage>();
        return services;
    }
}
