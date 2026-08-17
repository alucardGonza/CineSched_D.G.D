using Windows.ApplicationModel.DataTransfer;

namespace CineSched.App.Services;

public sealed class ClipboardService
{
    public Result<Unit> CopyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result<Unit>.Failure("clipboard.empty", "There is no text to copy.");
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception exception)
        {
            return Result<Unit>.Failure("clipboard.failed", exception.Message);
        }
    }
}
