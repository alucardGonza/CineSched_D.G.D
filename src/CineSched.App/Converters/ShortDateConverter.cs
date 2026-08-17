using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace CineSched.App.Converters;

public sealed class ShortDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DateTimeOffset date
            ? date.ToString("d", CultureInfo.CurrentCulture)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class LocalizedEnumConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, string> Spanish = new Dictionary<string, string>
    {
        ["English"] = "Inglés",
        ["Spanish"] = "Español",
        ["System"] = "Sistema",
        ["Blue"] = "Azul",
        ["Green"] = "Verde",
        ["Yellow"] = "Amarillo",
        ["Light"] = "Claro",
        ["Dark"] = "Oscuro",
        ["Default"] = "Predeterminado",
        ["ScriptOrder"] = "Orden de guion",
        ["SceneNumber"] = "Número de escena",
        ["Location"] = "Locación",
        ["Title"] = "Título",
        ["DayNight"] = "Día / Noche",
        ["Pages"] = "Páginas",
        ["EstimatedTime"] = "Tiempo estimado",
        ["Schedule"] = "Calendario",
        ["Stripboard"] = "Tiras",
        ["OneLine"] = "Una línea",
        ["Dood"] = "DOOD",
        ["Breakdown"] = "Desglose",
        ["CallSheet"] = "Orden del día"
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var text = value?.ToString() ?? string.Empty;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" && Spanish.TryGetValue(text, out var translated)
            ? translated
            : text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
