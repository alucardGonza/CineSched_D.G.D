namespace CineSched.App.Services;

public sealed class RecentFilesService(PreferencesService preferences)
{
    private const string Key = "recent-files";

    public IReadOnlyList<string> Get() => preferences.Get(Key, Array.Empty<string>());

    public async ValueTask AddAsync(StorageFile file, CancellationToken cancellationToken = default)
    {
        var identity = string.IsNullOrWhiteSpace(file.Path) ? file.Name : file.Path;
        var recent = Get().Where(value => !string.Equals(value, identity, StringComparison.OrdinalIgnoreCase)).ToList();
        recent.Insert(0, identity);
        await preferences.SetAsync(Key, recent.Take(10).ToArray(), cancellationToken);
    }

    public async ValueTask RemoveAsync(string identity, CancellationToken cancellationToken = default)
    {
        var recent = Get().Where(value => !string.Equals(value, identity, StringComparison.OrdinalIgnoreCase)).ToArray();
        await preferences.SetAsync(Key, recent, cancellationToken);
    }

    public async ValueTask<Result<StorageFile>> ResolveAsync(
        string identity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (string.IsNullOrWhiteSpace(identity) || !Path.IsPathRooted(identity))
            {
                await RemoveAsync(identity, cancellationToken);
                return Result<StorageFile>.Failure("recent.stale", "The recent file no longer has an accessible path.");
            }

            var file = await StorageFile.GetFileFromPathAsync(identity);
            return Result<StorageFile>.Success(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await RemoveAsync(identity, cancellationToken);
            return Result<StorageFile>.Failure("recent.stale", exception.Message);
        }
    }
}
