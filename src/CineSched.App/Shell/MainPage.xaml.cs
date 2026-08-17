namespace CineSched.App.Shell;

public sealed partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel, CineSched.App.Services.DialogService dialogs)
    {
        InitializeComponent();
        DataContext = viewModel;
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Loaded += (_, _) =>
        {
            if (XamlRoot is { } root) dialogs.Attach(root);
        };
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        Workspace.SelectedIndex = tag switch
        {
            MenuDefinitions.Calendar => 0,
            MenuDefinitions.Stripboard => 1,
            MenuDefinitions.Production => 2,
            MenuDefinitions.Reports => 3,
            _ => 0
        };
    }

    private void Boneyard_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        var ids = args.Items.OfType<CineSched.Core.Features.Scenes.Scene>().Select(scene => scene.Id);
        args.Data.SetText(string.Join(';', ids));
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void CalendarDay_Drop(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: ShootDay day } ||
            !args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text) ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var raw = await args.DataView.GetTextAsync();
        var ids = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
        viewModel.MoveScenesToDay(ids, day);
        args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }
}
