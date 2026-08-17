using System.Diagnostics.CodeAnalysis;
using CineSched.App.Shell;

namespace CineSched.App;

public partial class App : Application
{
    public App() => InitializeComponent();

    public Window? MainWindow { get; private set; }

    public IHost? Host { get; private set; }

    [SuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Uno hosting is configured with statically registered application services.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host.ConfigureServices((_, services) =>
            {
                ServiceRegistration.AddCineSched(services);
            }));

        MainWindow = builder.Window;
        Host = builder.Build();
        MainWindow.Content = Host.Services.GetRequiredService<MainPage>();
        MainWindow.Activate();
    }
}
