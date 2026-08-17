using Uno.UI.Hosting;

namespace CineSched.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            var configuredAppData = Environment.GetEnvironmentVariable("CINESCHED_APPDATA_PATH");
            Uno.WinRTFeatureConfiguration.ApplicationData.ApplicationDataPathOverride =
                string.IsNullOrWhiteSpace(configuredAppData)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CineSched")
                    : Path.GetFullPath(configuredAppData);
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
