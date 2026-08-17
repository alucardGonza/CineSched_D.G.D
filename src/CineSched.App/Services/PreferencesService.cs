using System.Text.Json;

namespace CineSched.App.Services;

public sealed class PreferencesService
{
    public T Get<T>(string key, T fallback)
    {
        try
        {
            var value = ApplicationData.Current.LocalSettings.Values[key] as string;
            return value is null ? fallback : JsonSerializer.Deserialize<T>(value) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public ValueTask SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationData.Current.LocalSettings.Values[key] = JsonSerializer.Serialize(value);
        return ValueTask.CompletedTask;
    }
}
