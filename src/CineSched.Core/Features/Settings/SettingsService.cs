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
            ,["ui.breakdownBrowser"] = ("Breakdown browser", "Navegador de desglose")
            ,["ui.lockSchedule"] = ("Lock schedule", "Bloquear programación")
            ,["ui.unlockSchedule"] = ("Unlock schedule", "Desbloquear programación")
            ,["ui.edit"] = ("Edit", "Editar")
            ,["ui.duplicate"] = ("Duplicate", "Duplicar")
            ,["ui.delete"] = ("Delete", "Eliminar")
            ,["ui.remove"] = ("Remove", "Quitar")
            ,["ui.eighths"] = ("eighths", "octavos")
            ,["ui.blackout"] = ("Blackout", "Bloqueo")
            ,["ui.allWeekdays"] = ("All weekdays", "Mismos días semanales")
            ,["ui.rosters"] = ("Rosters", "Repartos y equipos")
            ,["ui.callSheet"] = ("Call Sheet", "Orden del día")
            ,["ui.conflictsLock"] = ("Conflicts & Lock", "Conflictos y bloqueo")
            ,["ui.company"] = ("Company", "Compañía")
            ,["ui.director"] = ("Director", "Director")
            ,["ui.directorPhone"] = ("Director phone", "Teléfono del director")
            ,["ui.producer"] = ("Producer", "Productor")
            ,["ui.producerPhone"] = ("Producer phone", "Teléfono del productor")
            ,["ui.firstAd"] = ("1st AD", "1.er asistente de dirección")
            ,["ui.adPhone"] = ("AD phone", "Teléfono de AD")
            ,["ui.contact"] = ("Contact", "Contacto")
            ,["ui.defaultLunch"] = ("Default lunch", "Almuerzo predeterminado")
            ,["ui.cast"] = ("Cast", "Reparto")
            ,["ui.add"] = ("Add", "Agregar")
            ,["ui.availability"] = ("Availability", "Disponibilidad")
            ,["ui.crew"] = ("Crew", "Equipo")
            ,["ui.locations"] = ("Locations", "Locaciones")
            ,["ui.initializeFromDay"] = ("Initialize from day", "Inicializar desde el día")
            ,["ui.selectCrew"] = ("Select crew", "Seleccionar equipo")
            ,["ui.saveCallSheet"] = ("Save call sheet", "Guardar orden del día")
            ,["ui.generalCall"] = ("General call", "Llamado general")
            ,["ui.workDaySchedule"] = ("Work day schedule", "Horario de jornada")
            ,["ui.readyToShoot"] = ("Ready to shoot", "Listo para rodar")
            ,["ui.lunch"] = ("Lunch", "Almuerzo")
            ,["ui.snack"] = ("Snack", "Refrigerio")
            ,["ui.dinner"] = ("Dinner", "Cena")
            ,["ui.wrap"] = ("Wrap", "Fin de jornada")
            ,["ui.quoteOfDay"] = ("Quote of the day", "Frase del día")
            ,["ui.productionManagerContact"] = ("Production manager contact", "Contacto de gerencia de producción")
            ,["ui.adContact"] = ("AD contact", "Contacto de AD")
            ,["ui.weatherTemperature"] = ("Weather temperature", "Temperatura")
            ,["ui.weatherConditions"] = ("Weather conditions", "Condiciones climáticas")
            ,["ui.precipitationWind"] = ("Precipitation / wind", "Precipitación / viento")
            ,["ui.sunriseSunset"] = ("Sunrise / sunset", "Amanecer / atardecer")
            ,["ui.basecamp"] = ("Basecamp", "Campamento base")
            ,["ui.nearestHospital"] = ("Nearest hospital", "Hospital más cercano")
            ,["ui.castCalls"] = ("Cast calls", "Llamados del reparto")
            ,["ui.character"] = ("Character", "Personaje")
            ,["ui.actor"] = ("Actor", "Actor")
            ,["ui.scenes"] = ("Scenes", "Escenas")
            ,["ui.onSet"] = ("On set", "En set")
            ,["ui.notes"] = ("Notes", "Notas")
            ,["ui.conflicts"] = ("Conflicts", "Conflictos")
            ,["ui.lockBaseline"] = ("Lock baseline", "Bloquear línea base")
            ,["ui.scheduleLockChanges"] = ("Schedule lock changes", "Cambios desde el bloqueo")
            ,["status.ready"] = ("Ready", "Listo")
            ,["status.done"] = ("Done", "Listo")
            ,["status.productionSaved"] = ("Production setup saved", "Configuración de producción guardada")
        };
}
