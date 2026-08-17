using System.Globalization;

namespace CineSched.Core.Features.Settings;

public sealed class SettingsService
{
    private AppSettings _current = new();

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current => _current;

    public AppSettings Update(AppSettings settings)
    {
        _current = settings;
        Changed?.Invoke(this, settings);
        return settings;
    }

    public string GetText(string key)
    {
        var language = _current.Language;
        if (Translations.TryGetValue(key, out var translations))
        {
            return language == AppLanguage.Spanish ? translations.Es : translations.En;
        }

        return $"[{key}]";
    }

    public CultureInfo Culture => _current.Language == AppLanguage.Spanish
        ? CultureInfo.GetCultureInfo("es-ES")
        : CultureInfo.GetCultureInfo("en-US");

    public string FormatDate(DateTimeOffset value, string format = "D") => value.ToString(format, Culture);

    private static readonly IReadOnlyDictionary<string, (string En, string Es)> Translations =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["app.title"] = ("CineSched", "CineSched"),
            ["nav.calendar"] = ("Calendar", "Calendario"),
            ["nav.stripboard"] = ("Stripboard", "Tiras"),
            ["nav.production"] = ("Production", "Producción"),
            ["nav.reports"] = ("Reports", "Reportes"),
            ["project.new"] = ("New", "Nuevo"),
            ["project.open"] = ("Open", "Abrir"),
            ["project.save"] = ("Save", "Guardar"),
            ["project.saveAs"] = ("Save As", "Guardar como"),
            ["project.import"] = ("Import Script", "Importar guion"),
            ["project.undo"] = ("Undo", "Deshacer"),
            ["project.redo"] = ("Redo", "Rehacer"),
            ["nav.settings"] = ("Settings", "Ajustes"),
            ["settings.language"] = ("Language", "Idioma"),
            ["settings.theme"] = ("Theme", "Tema"),
            ["settings.colorMode"] = ("Color mode", "Modo de color"),
            ["reports.export"] = ("Export PDF", "Exportar PDF")
        };
}
