namespace CineSched.App.Services;

public sealed class AppLifecycleService(
    ProjectService projects,
    FileDialogService dialogs,
    RecentFilesService recentFiles,
    AutosaveService autosave)
{
    public StorageFile? AssociatedFile { get; private set; }

    public string? AssociatedFileIdentity => AssociatedFile is null ? null : GetIdentity(AssociatedFile);

    public void StartNewProject(DateTimeOffset now)
    {
        AssociatedFile = null;
        projects.NewProject(now);
    }

    public async ValueTask<Result<bool>> OpenFromPickerAsync(CancellationToken cancellationToken = default)
    {
        var file = await dialogs.PickProjectToOpenAsync(cancellationToken);
        if (file is null) return Result<bool>.Success(false);
        return await OpenAsync(file, cancellationToken);
    }

    public async ValueTask<Result<bool>> OpenAsync(StorageFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await file.OpenStreamForReadAsync();
            var opened = await projects.OpenAsync(stream, cancellationToken);
            if (!opened.IsSuccess)
            {
                return Result<bool>.Failure(opened.Error!.Code, opened.Error.Message, opened.Error.Details);
            }

            AssociatedFile = file;
            await recentFiles.AddAsync(file, cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await recentFiles.RemoveAsync(GetIdentity(file), cancellationToken);
            return Result<bool>.Failure("project.read-failed", exception.Message);
        }
    }

    public async ValueTask<Result<bool>> OpenRecentAsync(
        string identity,
        CancellationToken cancellationToken = default)
    {
        var resolved = await recentFiles.ResolveAsync(identity, cancellationToken);
        if (!resolved.IsSuccess)
            return Result<bool>.Failure(resolved.Error!.Code, resolved.Error.Message, resolved.Error.Details);
        return await OpenAsync(resolved.Value!, cancellationToken);
    }

    public async ValueTask<Result<bool>> OpenRecoveryAsync(
        StorageFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await file.OpenStreamForReadAsync();
            var opened = await projects.OpenAsync(stream, cancellationToken);
            if (!opened.IsSuccess)
                return Result<bool>.Failure(opened.Error!.Code, opened.Error.Message, opened.Error.Details);
            AssociatedFile = null;
            autosave.DismissRecovery();
            projects.Update(_ => { }, "project.recovered", structural: false);
            return Result<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<bool>.Failure("project.read-failed", exception.Message);
        }
    }

    public async ValueTask<Result<bool>> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (AssociatedFile is null)
        {
            return await SaveAsAsync(cancellationToken);
        }

        return await SaveToAsync(AssociatedFile, associateOnSuccess: false, cancellationToken);
    }

    public async ValueTask<Result<bool>> SaveAsAsync(CancellationToken cancellationToken = default)
    {
        var suggestedName = SanitizeFileName(projects.GetSnapshot().Document.ProjectTitle);
        var file = await dialogs.PickProjectToSaveAsync(suggestedName, cancellationToken);
        if (file is null) return Result<bool>.Success(false);
        return await SaveToAsync(file, associateOnSuccess: true, cancellationToken);
    }

    private async ValueTask<Result<bool>> SaveToAsync(
        StorageFile file,
        bool associateOnSuccess,
        CancellationToken cancellationToken)
    {
        var snapshot = projects.GetSnapshot();
        var path = file.Path;
        string? temporaryPath = null;
        try
        {
            Result<Unit> written;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var directory = Path.GetDirectoryName(path)
                    ?? throw new IOException("The destination directory is unavailable.");
                temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
                await using (var temporary = new FileStream(
                                 temporaryPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    written = await projects.WriteAsync(snapshot.Document, temporary, cancellationToken);
                }

                if (written.IsSuccess)
                {
                    File.Move(temporaryPath, path, overwrite: true);
                    temporaryPath = null;
                }
            }
            else
            {
                await using var destination = await file.OpenStreamForWriteAsync();
                destination.SetLength(0);
                written = await projects.WriteAsync(snapshot.Document, destination, cancellationToken);
            }

            if (!written.IsSuccess)
            {
                return Result<bool>.Failure(written.Error!.Code, written.Error.Message, written.Error.Details);
            }

            projects.MarkSaved(snapshot.Revision);
            await autosave.SaveNowAsync(snapshot, markRecoverable: false, cancellationToken: cancellationToken);
            if (associateOnSuccess) AssociatedFile = file;
            await recentFiles.AddAsync(file, cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<bool>.Failure("project.write-failed", exception.Message);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static string GetIdentity(StorageFile file) =>
        string.IsNullOrWhiteSpace(file.Path) ? file.Name : file.Path;

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
        return string.IsNullOrWhiteSpace(value) ? "Untitled Movie" : value;
    }
}
