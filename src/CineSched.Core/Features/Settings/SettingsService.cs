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
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
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
            ["settings.sidebar"] = ("Keep sidebar open", "Mantener barra lateral abierta"),
            ["settings.holdDays"] = ("Include hold days in DOOD", "Incluir días hold en DOOD"),
            ["reports.export"] = ("Export PDF", "Exportar PDF"),
            ["menu.file"] = ("File", "Archivo"),
            ["menu.edit"] = ("Edit", "Editar"),
            ["ui.recent"] = ("Recent projects", "Proyectos recientes"),
            ["ui.boneyard"] = ("Boneyard", "Sin programar"),
            ["ui.scene.add"] = ("+ Scene", "+ Escena"),
            ["ui.breakdown"] = ("Breakdown", "Desglose"),
            ["ui.copy"] = ("Copy", "Copiar"),
            ["ui.search"] = ("Search title, cast or summary", "Buscar título, reparto o resumen"),
            ["ui.sort"] = ("Sort", "Ordenar"),
            ["ui.sendToDay"] = ("Send to selected day", "Enviar al día seleccionado"),
            ["ui.start"] = ("Start", "Inicio"),
            ["ui.end"] = ("End", "Fin"),
            ["ui.shift"] = ("Shift existing schedule", "Desplazar programación existente"),
            ["ui.applyRange"] = ("Apply range", "Aplicar rango"),
            ["ui.stripboard"] = ("Selected day stripboard", "Tiras del día seleccionado"),
            ["ui.banner.add"] = ("+ Banner", "+ Aviso"),
            ["ui.meal.add"] = ("+ Meal", "+ Comida"),
            ["ui.event.add"] = ("+ Event", "+ Evento"),
            ["ui.productionSetup"] = ("Production Setup", "Configuración de producción"),
            ["ui.saveProduction"] = ("Save production", "Guardar producción"),
            ["ui.exportReport"] = ("Export report", "Exportar reporte")
        };
}
