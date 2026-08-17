using Windows.Storage.Pickers;

namespace CineSched.App.Services;

public sealed class FileDialogService
{
    public ValueTask<StorageFile?> PickProjectToOpenAsync(CancellationToken cancellationToken = default) =>
        PickOpenAsync([".cinesched", ".json"], cancellationToken);

    public ValueTask<StorageFile?> PickScriptToImportAsync(CancellationToken cancellationToken = default) =>
        PickOpenAsync([".fdx", ".xml", ".fountain", ".md", ".spmd", ".highland"], cancellationToken);

    public ValueTask<StorageFile?> PickProjectToSaveAsync(string suggestedName, CancellationToken cancellationToken = default) =>
        PickSaveAsync(suggestedName, "CineSched project", [".cinesched"], cancellationToken);

    public ValueTask<StorageFile?> PickReportToSaveAsync(string suggestedName, CancellationToken cancellationToken = default) =>
        PickSaveAsync(suggestedName, "PDF document", [".pdf"], cancellationToken);

    private static async ValueTask<StorageFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow(picker);
        return await picker.PickSingleFileAsync();
    }

    private static async ValueTask<StorageFile?> PickSaveAsync(
        string suggestedName,
        string description,
        IReadOnlyList<string> extensions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName
        };
        picker.FileTypeChoices.Add(description, extensions.ToList());
        InitializeWithWindow(picker);
        return await picker.PickSaveFileAsync();
    }

    private static void InitializeWithWindow(object picker)
    {
        if (!OperatingSystem.IsWindows() || App.Current is not App { MainWindow: not null } app) return;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
    }
}
