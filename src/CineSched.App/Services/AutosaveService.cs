namespace CineSched.App.Services;

public sealed class AutosaveService(ProjectService projects) : IDisposable
{
    private CancellationTokenSource? _pending;

    public void Start() => projects.Changed += OnProjectChanged;

    public void Dispose()
    {
        projects.Changed -= OnProjectChanged;
        _pending?.Cancel();
        _pending?.Dispose();
    }

    public async ValueTask<StorageFile?> GetRecoveryFileAsync()
    {
        try
        {
            return await ApplicationData.Current.LocalFolder.GetFileAsync("autosave.cinesched");
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask<Result<Unit>> SaveNowAsync(
        ProjectSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var folderPath = ApplicationData.Current.LocalFolder.Path;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Result<Unit>.Failure("project.write-failed", "The application data folder is unavailable.");
        }

        var destinationPath = Path.Combine(folderPath, "autosave.cinesched");
        var temporaryPath = Path.Combine(folderPath, $"autosave.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var result = await projects.WriteAsync(snapshot.Document, stream, cancellationToken);
                if (!result.IsSuccess) return result;
            }

            if (projects.Revision != snapshot.Revision)
            {
                return Result<Unit>.Failure("project.autosave-obsolete", "A newer project revision superseded this autosave.");
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            temporaryPath = string.Empty;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<Unit>.Failure("project.write-failed", exception.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private void OnProjectChanged(object? sender, ProjectChangedEvent change)
    {
        if (!change.IsDirty) return;
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(_pending.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await SaveNowAsync(projects.GetSnapshot(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
