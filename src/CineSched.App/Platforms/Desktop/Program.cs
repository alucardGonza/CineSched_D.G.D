using Uno.UI.Hosting;

namespace CineSched.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            Uno.WinRTFeatureConfiguration.ApplicationData.ApplicationDataPathOverride = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CineSched");
        }

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
