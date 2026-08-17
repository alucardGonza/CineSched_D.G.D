using CineSched.App.Services;
using CineSched.Core.Features.Reports;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.ScriptImport;

namespace CineSched.App.Shell;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProjectService _projects;
    private readonly SceneService _scenes;
    private readonly SchedulingService _scheduling;
    private readonly ScriptImportService _imports;
    private readonly ReportService _reports;
    private readonly FileDialogService _dialogs;
    private readonly DialogService _messages;
    private readonly AppLifecycleService _lifecycle;
    private readonly SettingsService _settings;
    private readonly PreferencesService _preferences;
    private ShootDay? _selectedDay;
    private Scene? _selectedBoneyardScene;
    private string _projectTitle = string.Empty;
    private string _status = "Ready";
    private ReportKind _selectedReportKind = ReportKind.Schedule;
    private string _searchQuery = string.Empty;

    public MainViewModel(
        ProjectService projects,
        SceneService scenes,
        SchedulingService scheduling,
        ScriptImportService imports,
        ReportService reports,
        FileDialogService dialogs,
        DialogService messages,
        AppLifecycleService lifecycle,
        SettingsService settings,
        PreferencesService preferences,
        AutosaveService autosave)
    {
        _projects = projects;
        _scenes = scenes;
        _scheduling = scheduling;
        _imports = imports;
        _reports = reports;
        _dialogs = dialogs;
        _messages = messages;
        _lifecycle = lifecycle;
        _settings = settings;
        _preferences = preferences;
        _settings.Update(_preferences.Get("app-settings", new AppSettings()));
        _projects.Changed += (_, _) => Refresh();
        autosave.Start();

        NewProjectCommand = new AsyncRelayCommand(NewProjectAsync);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
        SaveAsProjectCommand = new AsyncRelayCommand(SaveAsProjectAsync);
        ImportScriptCommand = new AsyncRelayCommand(ImportScriptAsync);
        AddSceneCommand = new RelayCommand(AddScene);
        SendToDayCommand = new RelayCommand(SendToSelectedDay, CanSendToSelectedDay);
        UndoCommand = new RelayCommand(() => SetStatus(_scheduling.Undo()), () => _scheduling.CanUndo);
        RedoCommand = new RelayCommand(() => SetStatus(_scheduling.Redo()), () => _scheduling.CanRedo);
        ExportReportCommand = new AsyncRelayCommand(ExportReportAsync);
        Refresh();
    }

    public ObservableCollection<ShootDay> ShootDays { get; } = [];
    public ObservableCollection<Scene> Boneyard { get; } = [];
    public ObservableCollection<Scene> SelectedDayScenes { get; } = [];
    public IReadOnlyList<ReportKind> ReportKinds { get; } = Enum.GetValues<ReportKind>();
    public IReadOnlyList<AppLanguage> Languages { get; } = Enum.GetValues<AppLanguage>();
    public IReadOnlyList<CineSchedAppTheme> Themes { get; } = Enum.GetValues<CineSchedAppTheme>();
    public IReadOnlyList<ColorMode> ColorModes { get; } = Enum.GetValues<ColorMode>();

    public event EventHandler<AppSettings>? SettingsChanged;

    public IAsyncRelayCommand NewProjectCommand { get; }
    public IAsyncRelayCommand OpenProjectCommand { get; }
    public IAsyncRelayCommand SaveProjectCommand { get; }
    public IAsyncRelayCommand SaveAsProjectCommand { get; }
    public IAsyncRelayCommand ImportScriptCommand { get; }
    public IRelayCommand AddSceneCommand { get; }
    public IRelayCommand SendToDayCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IAsyncRelayCommand ExportReportCommand { get; }

    public ShootDay? SelectedDay
    {
        get => _selectedDay;
        set
        {
            if (!SetProperty(ref _selectedDay, value)) return;
            RefreshSelectedDay();
            SendToDayCommand.NotifyCanExecuteChanged();
        }
    }

    public Scene? SelectedBoneyardScene
    {
        get => _selectedBoneyardScene;
        set
        {
            if (!SetProperty(ref _selectedBoneyardScene, value)) return;
            SendToDayCommand.NotifyCanExecuteChanged();
        }
    }

    public string ProjectTitle
    {
        get => _projectTitle;
        set
        {
            if (!SetProperty(ref _projectTitle, value)) return;
            if (!string.Equals(_projects.CurrentDocument.ProjectTitle, value, StringComparison.Ordinal))
            {
                _projects.Update(document => document.ProjectTitle = value, "project.title-updated");
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ReportKind SelectedReportKind
    {
        get => _selectedReportKind;
        set => SetProperty(ref _selectedReportKind, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            RefreshBoneyard();
        }
    }

    public AppLanguage SelectedLanguage
    {
        get => _settings.Current.Language;
        set => ChangeSettings(_settings.Current with { Language = value });
    }

    public CineSchedAppTheme SelectedTheme
    {
        get => _settings.Current.Theme;
        set => ChangeSettings(_settings.Current with { Theme = value });
    }

    public ColorMode SelectedColorMode
    {
        get => _settings.Current.ColorMode;
        set => ChangeSettings(_settings.Current with { ColorMode = value });
    }

    public Brush AccentBrush => new SolidColorBrush(_settings.Current.Theme switch
    {
        CineSchedAppTheme.Green => Windows.UI.Color.FromArgb(255, 22, 163, 74),
        CineSchedAppTheme.Yellow => Windows.UI.Color.FromArgb(255, 202, 138, 4),
        CineSchedAppTheme.System => (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"],
        _ => Windows.UI.Color.FromArgb(255, 37, 99, 235)
    });

    public string CalendarLabel => _settings.GetText("nav.calendar");
    public string StripboardLabel => _settings.GetText("nav.stripboard");
    public string ProductionLabel => _settings.GetText("nav.production");
    public string ReportsLabel => _settings.GetText("nav.reports");
    public string SettingsLabel => _settings.GetText("nav.settings");
    public string NewLabel => _settings.GetText("project.new");
    public string OpenLabel => _settings.GetText("project.open");
    public string SaveLabel => _settings.GetText("project.save");
    public string SaveAsLabel => _settings.GetText("project.saveAs");
    public string ImportLabel => _settings.GetText("project.import");
    public string UndoLabel => _settings.GetText("project.undo");
    public string RedoLabel => _settings.GetText("project.redo");
    public string LanguageLabel => _settings.GetText("settings.language");
    public string ThemeLabel => _settings.GetText("settings.theme");
    public string ColorModeLabel => _settings.GetText("settings.colorMode");
    public string ExportLabel => _settings.GetText("reports.export");

    public void MoveScenesToDay(IReadOnlyList<Guid> sceneIds, ShootDay target)
    {
        if (sceneIds.Count == 0) return;
        SetStatus(_scheduling.MoveScenes(new MoveScenesRequest(sceneIds, null, target.Id, target.Scenes.Count)));
    }

    private async Task NewProjectAsync()
    {
        if (_projects.IsDirty && !await _messages.ConfirmAsync(
                "Create a new project?", "Unsaved changes will remain only in the recovery autosave.", "Create"))
        {
            return;
        }

        _lifecycle.StartNewProject(DateTimeOffset.Now);
        Status = "New project created";
    }

    private async Task OpenProjectAsync()
    {
        if (_projects.IsDirty && !await _messages.ConfirmAsync(
                "Open another project?", "The current project has unsaved changes.", "Open"))
        {
            return;
        }

        var result = await _lifecycle.OpenFromPickerAsync();
        if (result.IsSuccess && result.Value)
        {
            Status = $"Opened {_lifecycle.AssociatedFile?.Name}";
        }
        else if (!result.IsSuccess) Status = result.Error!.Message;
    }

    private async Task SaveProjectAsync()
    {
        var result = await _lifecycle.SaveAsync();
        if (result.IsSuccess && result.Value)
            Status = $"Saved {_lifecycle.AssociatedFile?.Name}";
        else if (!result.IsSuccess)
            Status = result.Error!.Message;
    }

    private async Task SaveAsProjectAsync()
    {
        var result = await _lifecycle.SaveAsAsync();
        if (result.IsSuccess && result.Value)
            Status = $"Saved {_lifecycle.AssociatedFile?.Name}";
        else if (!result.IsSuccess)
            Status = result.Error!.Message;
    }

    private async Task ImportScriptAsync()
    {
        var file = await _dialogs.PickScriptToImportAsync();
        if (file is null) return;
        await using var stream = await file.OpenStreamForReadAsync();
        var parsed = await _imports.ImportAsync(stream, Path.GetExtension(file.Name));
        if (!parsed.IsSuccess)
        {
            Status = parsed.Error!.Message;
            return;
        }

        var current = _projects.GetSnapshot().Document;
        var hasContent = current.AllScenes.Count > 0 || current.ShootDays.Any(day => day.Scenes.Count > 0) ||
                         !string.Equals(current.ProjectTitle, "Untitled Movie", StringComparison.Ordinal);
        if (hasContent && !await _messages.ConfirmAsync(
                "Import parsed scenes?", $"Add {parsed.Value!.Scenes.Count} scenes to the current Boneyard?", "Import"))
        {
            Status = "Import cancelled";
            return;
        }

        var committed = _imports.Commit(parsed.Value!);
        Status = committed.IsSuccess ? $"Imported {committed.Value} scenes" : committed.Error!.Message;
    }

    private void AddScene()
    {
        var number = (_projects.GetSnapshot().Document.AllScenes.Count + 1).ToString();
        var result = _scenes.CreateScene(new SceneInput("NEW SCENE", number, "1", "15"));
        SetStatus(result);
    }

    private bool CanSendToSelectedDay() => SelectedDay is not null && SelectedBoneyardScene is not null;

    private void SendToSelectedDay()
    {
        if (SelectedDay is null || SelectedBoneyardScene is null) return;
        var result = _scheduling.MoveScenes(new MoveScenesRequest(
            [SelectedBoneyardScene.Id], null, SelectedDay.Id, SelectedDay.Scenes.Count));
        SetStatus(result);
    }

    private async Task ExportReportAsync()
    {
        var file = await _dialogs.PickReportToSaveAsync($"{SafeName(ProjectTitle)}-{SelectedReportKind}");
        if (file is null) return;
        await using var stream = await file.OpenStreamForWriteAsync();
        stream.SetLength(0);
        var language = _settings.Current.Language == AppLanguage.Spanish ? ReportLanguage.Spanish : ReportLanguage.English;
        var request = new ReportRequest(_projects.GetSnapshot().Document, language, _settings.Current.IncludeHoldDays, SelectedDay);
        var result = await _reports.GenerateAsync(SelectedReportKind, request, stream);
        Status = result.IsSuccess ? $"Exported {file.Name}" : result.Error!.Message;
    }

    private void Refresh()
    {
        var document = _projects.GetSnapshot().Document;
        if (_projectTitle != document.ProjectTitle)
        {
            _projectTitle = document.ProjectTitle;
            OnPropertyChanged(nameof(ProjectTitle));
        }

        var selectedId = SelectedDay?.Id;
        Replace(ShootDays, document.ShootDays.OrderBy(day => day.Date));
        RefreshBoneyard();
        _selectedDay = ShootDays.FirstOrDefault(day => day.Id == selectedId) ?? ShootDays.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedDay));
        RefreshSelectedDay();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectedDay() => Replace(SelectedDayScenes, SelectedDay?.Scenes ?? []);

    private void RefreshBoneyard()
    {
        var values = string.IsNullOrWhiteSpace(SearchQuery)
            ? _scenes.SortBoneyard(BoneyardSort.Default)
            : _scenes.SearchScenes(SearchQuery).Select(result => result.Scene)
                .GroupBy(scene => scene.Id).Select(group => group.First()).ToList();
        Replace(Boneyard, values);
    }

    private void SetStatus<T>(Result<T> result) => Status = result.IsSuccess ? "Done" : result.Error!.Message;

    private void ChangeSettings(AppSettings settings)
    {
        if (settings == _settings.Current) return;
        _settings.Update(settings);
        _ = _preferences.SetAsync("app-settings", settings);
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedColorMode));
        OnPropertyChanged(nameof(AccentBrush));
        foreach (var property in new[]
                 {
                     nameof(CalendarLabel), nameof(StripboardLabel), nameof(ProductionLabel), nameof(ReportsLabel),
                     nameof(SettingsLabel), nameof(NewLabel), nameof(OpenLabel), nameof(SaveLabel), nameof(SaveAsLabel),
                     nameof(ImportLabel), nameof(UndoLabel), nameof(RedoLabel), nameof(LanguageLabel), nameof(ThemeLabel),
                     nameof(ColorModeLabel), nameof(ExportLabel)
                 })
            OnPropertyChanged(property);
        SettingsChanged?.Invoke(this, settings);
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '-' : character)).Trim();
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values) collection.Add(value);
    }
}
