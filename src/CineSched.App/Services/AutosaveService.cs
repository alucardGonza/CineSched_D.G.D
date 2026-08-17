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
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "autosave.cinesched", CreationCollisionOption.ReplaceExisting);
            await using var stream = await file.OpenStreamForWriteAsync();
            await projects.WriteAsync(projects.GetSnapshot().Document, stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
