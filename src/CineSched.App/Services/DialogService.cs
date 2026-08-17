namespace CineSched.App.Services;

public sealed class DialogService
{
    private XamlRoot? _xamlRoot;

    public void Attach(XamlRoot xamlRoot) => _xamlRoot = xamlRoot;

    public async ValueTask<bool> ConfirmAsync(
        string title,
        string message,
        string primaryButton = "Continue",
        string closeButton = "Cancel")
    {
        if (_xamlRoot is null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButton,
            CloseButtonText = closeButton,
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async ValueTask ShowErrorAsync(string title, string message)
    {
        if (_xamlRoot is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
