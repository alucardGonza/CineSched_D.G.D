using CineSched.Core.Features.Production;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.Conflicts;
using CineSched.Core.Features.ScheduleLock;
using CineSched.Core.Features.Stripboard;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace CineSched.App.Shell;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _viewModel;
    private readonly CineSched.App.Services.DialogService _dialogs;

    public MainPage(
        MainViewModel viewModel,
        CineSched.App.Services.DialogService dialogs,
        CineSched.App.Services.AutosaveService autosave,
        CineSched.App.Services.AppLifecycleService lifecycle)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogs = dialogs;
        DataContext = viewModel;
        Navigation.SelectedItem = Navigation.MenuItems[0];
        ApplySettings(viewModel.SelectedColorMode);
        viewModel.SettingsChanged += (_, settings) => ApplySettings(settings.ColorMode);
        Loaded += async (_, _) =>
        {
            if (XamlRoot is { } root) dialogs.Attach(root);
            if (_viewModel.ShootDays.Count > 0)
            {
                RangeStart.Date = _viewModel.ShootDays.Min(day => day.Date);
                RangeEnd.Date = _viewModel.ShootDays.Max(day => day.Date);
            }
            var recovery = await autosave.GetRecoveryFileAsync();
            if (recovery is null) return;
            if (await dialogs.ConfirmAsync("Recover unsaved project?", "CineSched found changes from the previous session.", "Recover"))
            {
                var result = await lifecycle.OpenRecoveryAsync(recovery);
                if (!result.IsSuccess) await dialogs.ShowErrorAsync("Recovery failed", result.Error!.Message);
            }
            else
            {
                autosave.DismissRecovery();
            }
        };
        ConfigureKeyboardAccelerators();
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
            MenuDefinitions.Settings => 4,
            _ => 0
        };
    }

    private void RecentFiles_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is string identity && _viewModel.OpenRecentCommand.CanExecute(identity))
        {
            _viewModel.OpenRecentCommand.Execute(identity);
        }
    }

    private void ApplySettings(ColorMode colorMode) => RequestedTheme = colorMode switch
    {
        ColorMode.Light => ElementTheme.Light,
        ColorMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private void Boneyard_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        var ids = args.Items.OfType<CineSched.Core.Features.Scenes.Scene>().Select(scene => scene.Id);
        args.Data.SetText("scenes:" + string.Join(';', ids));
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void ShootDays_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (args.Items.OfType<ShootDay>().FirstOrDefault() is not { } day) return;
        args.Data.SetText($"day:{day.Id}");
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void DayScenes_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        var ids = args.Items.OfType<Scene>().Select(scene => scene.Id);
        args.Data.SetText("scenes:" + string.Join(';', ids));
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void Stripboard_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        var ids = args.Items.OfType<Scene>().Select(scene => scene.Id);
        args.Data.SetText("reorder:" + string.Join(';', ids));
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void Move_DragOver(object sender, DragEventArgs args)
    {
        if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
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
        if (raw.StartsWith("day:", StringComparison.Ordinal) &&
            Guid.TryParse(raw[4..], out var sourceDayId) &&
            _viewModel.ShootDays.FirstOrDefault(candidate => candidate.Id == sourceDayId) is { } sourceDay)
        {
            viewModel.MoveWholeDay(sourceDay, day);
            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            return;
        }

        if (!raw.StartsWith("scenes:", StringComparison.Ordinal)) return;
        var ids = raw[7..].Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
        viewModel.MoveScenesToDay(ids, day);
        args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void StripScene_Drop(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: Scene target } || _viewModel.SelectedDay is not { } day ||
            !args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) return;
        var raw = await args.DataView.GetTextAsync();
        if (!raw.StartsWith("reorder:", StringComparison.Ordinal)) return;
        var ids = raw[8..].Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).ToList();
        var targetIndex = day.Scenes.FindIndex(scene => scene.Id == target.Id);
        if (targetIndex >= 0) _viewModel.ReorderScenes(day, ids, targetIndex);
        args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void ChangeRange_Click(object sender, RoutedEventArgs args) =>
        _viewModel.ChangeDateRange(RangeStart.Date, RangeEnd.Date, ShiftSchedule.IsChecked == true);

    private void RemoveFromDay_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: Scene scene }) return;
        var day = _viewModel.ShootDays.FirstOrDefault(candidate => candidate.Scenes.Any(value => value.Id == scene.Id));
        if (day is not null) _viewModel.RemoveSceneFromDay(day, scene);
    }

    private void ToggleWeekdayBlackout_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ShootDay day }) _viewModel.ToggleBlackout(day, matchingWeekday: true);
    }

    private void JumpToConflict_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ScheduleConflict conflict })
            SelectCalendarDay(conflict.ShootDayId);
    }

    private void JumpToLockChange_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: ScheduleLockChange change }) return;
        var date = change.AddedDays.Concat(change.RemovedDays).FirstOrDefault();
        var day = _viewModel.ShootDays.FirstOrDefault(candidate => candidate.Date.Date == date.Date);
        if (day is not null) SelectCalendarDay(day.Id);
    }

    private void SelectCalendarDay(Guid dayId)
    {
        var day = _viewModel.ShootDays.FirstOrDefault(candidate => candidate.Id == dayId);
        if (day is null) return;
        _viewModel.SelectedDay = day;
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Workspace.SelectedIndex = 0;
    }

    private async void AddScene_Click(object sender, RoutedEventArgs args) => await ShowSceneEditorAsync(null);

    private async void Scene_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: Scene scene }) await ShowSceneEditorAsync(scene);
    }

    private async void BreakdownBrowser_Click(object sender, RoutedEventArgs args)
    {
        var scenes = _viewModel.GetScriptOrder();
        if (scenes.Count == 0)
        {
            await _dialogs.ShowErrorAsync("Breakdown browser", "There are no scenes in the project.");
            return;
        }

        var list = new ListView
        {
            ItemsSource = scenes,
            DisplayMemberPath = nameof(Scene.DisplayTitle),
            SelectionMode = ListViewSelectionMode.Single,
            SelectedIndex = 0,
            Width = 520,
            MaxHeight = 580
        };
        if (await ShowEditorAsync("Breakdown browser — script order", list, "Edit selected") == ContentDialogResult.Primary &&
            list.SelectedItem is Scene scene)
        {
            await ShowSceneEditorAsync(scene);
        }
    }

    private async void EditScene_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: Scene scene }) await ShowSceneEditorAsync(scene);
    }

    private void DuplicateScene_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: Scene scene }) _viewModel.DuplicateScene(scene);
    }

    private async void DeleteScene_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: Scene scene }) return;
        if (await _dialogs.ConfirmAsync("Delete scene?", scene.DisplayTitle, "Delete")) _viewModel.DeleteScene(scene);
    }

    private void ToggleBlackout_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ShootDay day }) _viewModel.ToggleBlackout(day);
    }

    private async void AddBanner_Click(object sender, RoutedEventArgs args)
    {
        var panel = new StackPanel { Spacing = 8, Width = 420 };
        var type = AddCombo(panel, "Type", Enum.GetValues<BannerType>(), BannerType.Notice);
        var title = AddText(panel, "Title", "Notice");
        var note = AddText(panel, "Note", string.Empty, true);
        var duration = AddText(panel, "Duration in minutes", "15");
        var color = AddText(panel, "Color", "#2563EB");
        var start = AddText(panel, "Fixed start (optional)", string.Empty);
        if (await ShowEditorAsync("Add banner", panel, "Add") != ContentDialogResult.Primary) return;
        if (!int.TryParse(duration.Text, out var minutes) || minutes < 0)
        {
            await _dialogs.ShowErrorAsync("Invalid duration", "Duration must be a non-negative number of minutes.");
            return;
        }
        _viewModel.AddBanner((BannerType)type.SelectedItem, title.Text, note.Text, minutes, color.Text, start.Text);
    }

    private async void AddMeal_Click(object sender, RoutedEventArgs args)
    {
        var panel = new StackPanel { Spacing = 8, Width = 360 };
        var kind = AddCombo(panel, "Meal", Enum.GetValues<MealKind>(), MealKind.Lunch);
        var start = AddText(panel, "Start time", "01:30 PM");
        if (await ShowEditorAsync("Add meal", panel, "Add") == ContentDialogResult.Primary)
            _viewModel.AddMeal((MealKind)kind.SelectedItem, start.Text);
    }

    private async void AddEvent_Click(object sender, RoutedEventArgs args)
    {
        var panel = new StackPanel { Spacing = 8, Width = 360 };
        var title = AddText(panel, "Title", "Event");
        var start = AddText(panel, "Start time", "07:00 AM");
        var duration = AddText(panel, "Duration in minutes", "0");
        var color = AddText(panel, "Color", "#2563EB");
        if (await ShowEditorAsync("Add calendar event", panel, "Add") != ContentDialogResult.Primary) return;
        if (!int.TryParse(duration.Text, out var minutes) || minutes < 0)
        {
            await _dialogs.ShowErrorAsync("Invalid duration", "Duration must be a non-negative number of minutes.");
            return;
        }
        _viewModel.AddCalendarEvent(title.Text, start.Text, color.Text, minutes);
    }

    private async Task ShowSceneEditorAsync(Scene? scene)
    {
        var panel = new StackPanel { Spacing = 8, Width = 480 };
        var title = AddText(panel, "Slugline / title", scene?.Title ?? string.Empty);
        var number = AddText(panel, "Scene number", scene?.SceneNumber ?? string.Empty);
        var duration = AddText(panel, "Pages in eighths or fraction", scene?.Duration.ToString() ?? "1");
        var estimated = AddText(panel, "Estimated time", scene?.EstimatedTime.ToString() ?? "15");
        var dayNight = AddCombo(panel, "Day / Night", Enum.GetValues<DayNightType>(), scene?.DayNightType ?? DayNightType.Day);
        var cast = AddText(panel, "Cast (comma separated)", string.Join(", ", scene?.Cast ?? []));
        var summary = AddText(panel, "Summary", scene?.Summary ?? string.Empty, true);
        var realLocation = AddText(panel, "Real location", scene?.RealLocation ?? string.Empty);
        var locationAddress = AddText(panel, "Location address", scene?.LocationAddress ?? string.Empty);
        var extras = AddText(panel, "Extras (comma separated)", Csv(scene?.Extras));
        var props = AddText(panel, "Props (comma separated)", Csv(scene?.Props));
        var setDressing = AddText(panel, "Set dressing (comma separated)", Csv(scene?.SetDressing));
        var wardrobe = AddText(panel, "Wardrobe (comma separated)", Csv(scene?.Wardrobe));
        var makeupHair = AddText(panel, "Makeup / hair (comma separated)", Csv(scene?.MakeupHair));
        var vehicles = AddText(panel, "Vehicles (comma separated)", Csv(scene?.Vehicles));
        var equipment = AddText(panel, "Special equipment (comma separated)", Csv(scene?.SpecialEquipment));
        var stunts = AddText(panel, "Stunts (comma separated)", Csv(scene?.Stunts));
        var sfx = AddText(panel, "SFX (comma separated)", Csv(scene?.Sfx));
        var vfx = AddText(panel, "VFX (comma separated)", Csv(scene?.Vfx));
        var breakdownNotes = AddText(panel, "Breakdown notes", scene?.BreakdownNotes ?? string.Empty, true);
        if (await ShowEditorAsync(scene is null ? "New scene" : "Edit scene", panel, "Save") != ContentDialogResult.Primary) return;
        var input = new SceneInput(
            Title: title.Text,
            SceneNumber: number.Text,
            Duration: duration.Text,
            EstimatedTime: estimated.Text,
            DayNightType: (DayNightType)dayNight.SelectedItem,
            Cast: SplitCsv(cast.Text),
            Summary: summary.Text,
            RealLocation: realLocation.Text,
            LocationAddress: locationAddress.Text,
            Extras: SplitCsv(extras.Text),
            Props: SplitCsv(props.Text),
            SetDressing: SplitCsv(setDressing.Text),
            Wardrobe: SplitCsv(wardrobe.Text),
            MakeupHair: SplitCsv(makeupHair.Text),
            Vehicles: SplitCsv(vehicles.Text),
            SpecialEquipment: SplitCsv(equipment.Text),
            Stunts: SplitCsv(stunts.Text),
            Sfx: SplitCsv(sfx.Text),
            Vfx: SplitCsv(vfx.Text),
            BreakdownNotes: breakdownNotes.Text);
        var result = _viewModel.SaveScene(scene, input);
        if (!result.IsSuccess) await _dialogs.ShowErrorAsync("Scene validation", result.Error!.Message);
    }

    private async void AddCast_Click(object sender, RoutedEventArgs args) => await ShowCastEditorAsync(null);
    private async void EditCast_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CastMember member }) await ShowCastEditorAsync(member);
    }
    private async void DeleteCast_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CastMember member } && await _dialogs.ConfirmAsync("Remove cast member?", member.DisplayString, "Remove")) _viewModel.RemoveCast(member);
    }

    private async void EditAvailability_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: CastMember member }) return;
        var panel = new StackPanel { Spacing = 8, Width = 420 };
        var existing = new ComboBox
        {
            Header = "Existing unavailable ranges",
            ItemsSource = member.UnavailableRanges,
            DisplayMemberPath = nameof(DateRange.DisplayString),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var start = new DatePicker { Header = "Start", Date = DateTimeOffset.Now.Date };
        var end = new DatePicker { Header = "End", Date = DateTimeOffset.Now.Date };
        panel.Children.Add(existing);
        panel.Children.Add(start);
        panel.Children.Add(end);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Availability — {member.DisplayString}",
            Content = panel,
            PrimaryButtonText = "Add range",
            SecondaryButtonText = "Remove selected",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary && existing.SelectedItem is DateRange range)
        {
            _viewModel.RemoveUnavailableRange(member, range);
        }
        else if (result == ContentDialogResult.Primary)
        {
            var added = _viewModel.AddUnavailableRange(member, start.Date, end.Date);
            if (!added.IsSuccess) await _dialogs.ShowErrorAsync("Invalid availability", added.Error!.Message);
        }
    }

    private async Task ShowCastEditorAsync(CastMember? member)
    {
        var panel = new StackPanel { Spacing = 8, Width = 360 };
        var actor = AddText(panel, "Actor", member?.ActorName ?? string.Empty);
        var character = AddText(panel, "Character", member?.CharacterName ?? string.Empty);
        if (await ShowEditorAsync(member is null ? "Add cast" : "Edit cast", panel, "Save") != ContentDialogResult.Primary) return;
        if (member is null) _viewModel.AddCast(actor.Text, character.Text);
        else _viewModel.UpdateCast(member, actor.Text, character.Text);
    }

    private async void AddCrew_Click(object sender, RoutedEventArgs args) => await ShowCrewEditorAsync(null);
    private async void EditCrew_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CrewMember member }) await ShowCrewEditorAsync(member);
    }
    private async void DeleteCrew_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CrewMember member } && await _dialogs.ConfirmAsync("Remove crew member?", member.DisplayString, "Remove")) _viewModel.RemoveCrew(member);
    }

    private async Task ShowCrewEditorAsync(CrewMember? member)
    {
        var panel = new StackPanel { Spacing = 8, Width = 360 };
        var name = AddText(panel, "Name", member?.Name ?? string.Empty);
        var role = AddText(panel, "Role", member?.Role ?? string.Empty);
        var phone = AddText(panel, "Phone", member?.Phone ?? string.Empty);
        var daily = new CheckBox { Content = "Selected by default on new call sheets", IsChecked = member?.IsDailyDefault ?? false };
        panel.Children.Add(daily);
        if (await ShowEditorAsync(member is null ? "Add crew" : "Edit crew", panel, "Save") != ContentDialogResult.Primary) return;
        if (member is null) _viewModel.AddCrew(name.Text, role.Text, phone.Text, daily.IsChecked == true);
        else _viewModel.UpdateCrew(member, name.Text, role.Text, phone.Text, daily.IsChecked == true);
    }

    private async void AddLocation_Click(object sender, RoutedEventArgs args) => await ShowLocationEditorAsync(null);
    private async void EditLocation_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: Location location }) await ShowLocationEditorAsync(location);
    }
    private async void DeleteLocation_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: Location location } && await _dialogs.ConfirmAsync("Remove location?", location.Name, "Remove")) _viewModel.RemoveLocation(location);
    }

    private async Task ShowLocationEditorAsync(Location? location)
    {
        var panel = new StackPanel { Spacing = 8, Width = 360 };
        var name = AddText(panel, "Name", location?.Name ?? string.Empty);
        var address = AddText(panel, "Address", location?.Address ?? string.Empty);
        if (await ShowEditorAsync(location is null ? "Add location" : "Edit location", panel, "Save") != ContentDialogResult.Primary) return;
        if (location is null) _viewModel.AddLocation(name.Text, address.Text);
        else _viewModel.UpdateLocation(location, name.Text, address.Text);
    }

    private async void SelectCallSheetCrew_Click(object sender, RoutedEventArgs args)
    {
        if (_viewModel.SelectedDay is null) return;
        var selected = _viewModel.CallSheetDraft.CrewIDOverride?.ToHashSet() ?? [];
        var panel = new StackPanel { Spacing = 6, Width = 420 };
        foreach (var member in _viewModel.CrewRoster)
        {
            panel.Children.Add(new CheckBox
            {
                Content = member.DisplayString,
                Tag = member.Id,
                IsChecked = selected.Contains(member.Id)
            });
        }

        if (await ShowEditorAsync("Select call sheet crew", panel, "Apply") != ContentDialogResult.Primary) return;
        _viewModel.SetCallSheetCrew(panel.Children.OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true && checkBox.Tag is Guid)
            .Select(checkBox => (Guid)checkBox.Tag));
    }

    private async Task<ContentDialogResult> ShowEditorAsync(string title, UIElement content, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new ScrollViewer { Content = content, MaxHeight = 620 },
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync();
    }

    private static TextBox AddText(StackPanel panel, string header, string value, bool multiline = false)
    {
        var input = new TextBox { Header = header, Text = value, AcceptsReturn = multiline };
        if (multiline) input.Height = 90;
        panel.Children.Add(input);
        return input;
    }

    private static ComboBox AddCombo<T>(StackPanel panel, string header, IReadOnlyList<T> values, T selected)
    {
        var input = new ComboBox { Header = header, ItemsSource = values, SelectedItem = selected, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(input);
        return input;
    }

    private static string Csv(IEnumerable<string>? values) => string.Join(", ", values ?? []);

    private static string[] SplitCsv(string value) => value.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void ConfigureKeyboardAccelerators()
    {
        var primary = OperatingSystem.IsMacOS() ? VirtualKeyModifiers.Windows : VirtualKeyModifiers.Control;
        AddAccelerator(VirtualKey.N, primary, _viewModel.NewProjectCommand);
        AddAccelerator(VirtualKey.O, primary, _viewModel.OpenProjectCommand);
        AddAccelerator(VirtualKey.S, primary, _viewModel.SaveProjectCommand);
        AddAccelerator(VirtualKey.S, primary | VirtualKeyModifiers.Shift, _viewModel.SaveAsProjectCommand);
        AddAccelerator(VirtualKey.Z, primary, _viewModel.UndoCommand);
        AddAccelerator(VirtualKey.Y, primary, _viewModel.RedoCommand);
        AddAccelerator(VirtualKey.C, primary | VirtualKeyModifiers.Shift, _viewModel.CopySceneCommand);
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, System.Windows.Input.ICommand command)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            if (!command.CanExecute(null)) return;
            command.Execute(null);
            args.Handled = true;
        };
        KeyboardAccelerators.Add(accelerator);
    }

    private void Card_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (sender is UIElement element) AnimateOpacity(element, 0.82);
    }

    private void Card_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (sender is UIElement element) AnimateOpacity(element, 1);
    }

    private static void AnimateOpacity(UIElement element, double target)
    {
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(110)),
            EnableDependentAnimation = false
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
