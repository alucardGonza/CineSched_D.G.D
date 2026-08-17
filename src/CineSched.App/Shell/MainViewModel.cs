using CineSched.App.Services;
using CineSched.Core.Features.CallSheets;
using CineSched.Core.Features.Conflicts;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Reports;
using CineSched.Core.Features.ScheduleLock;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.ScriptImport;
using CineSched.Core.Features.Stripboard;

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
    private readonly StripboardService _stripboard;
    private readonly ProductionService _production;
    private readonly CallSheetService _callSheets;
    private readonly ConflictService _conflicts;
    private readonly ScheduleLockService _scheduleLock;
    private readonly ClipboardService _clipboard;
    private readonly RecentFilesService _recentFiles;
    private readonly AutosaveService _autosave;
    private ShootDay? _selectedDay;
    private Scene? _selectedBoneyardScene;
    private string _projectTitle = string.Empty;
    private string _status = "Ready";
    private ReportKind _selectedReportKind = ReportKind.Schedule;
    private string _searchQuery = string.Empty;
    private BoneyardSort _selectedBoneyardSort;
    private ProductionInfo _productionDraft = new();
    private CallSheetData _callSheetDraft = new();

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
        StripboardService stripboard,
        ProductionService production,
        CallSheetService callSheets,
        ConflictService conflicts,
        ScheduleLockService scheduleLock,
        ClipboardService clipboard,
        RecentFilesService recentFiles,
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
        _stripboard = stripboard;
        _production = production;
        _callSheets = callSheets;
        _conflicts = conflicts;
        _scheduleLock = scheduleLock;
        _clipboard = clipboard;
        _recentFiles = recentFiles;
        _autosave = autosave;
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
        SaveProductionCommand = new RelayCommand(SaveProduction);
        InitializeCallSheetCommand = new RelayCommand(InitializeCallSheet, () => SelectedDay is not null);
        SaveCallSheetCommand = new RelayCommand(SaveCallSheet, () => SelectedDay is not null);
        LockScheduleCommand = new RelayCommand(LockSchedule);
        UnlockScheduleCommand = new RelayCommand(UnlockSchedule);
        CopySceneCommand = new RelayCommand(CopySelectedScene, () => SelectedBoneyardScene is not null);
        OpenRecentCommand = new AsyncRelayCommand<string>(OpenRecentAsync);
        Refresh();
    }

    public ObservableCollection<ShootDay> ShootDays { get; } = [];
    public ObservableCollection<Scene> Boneyard { get; } = [];
    public ObservableCollection<Scene> SelectedDayScenes { get; } = [];
    public ObservableCollection<CastMember> CastRoster { get; } = [];
    public ObservableCollection<CrewMember> CrewRoster { get; } = [];
    public ObservableCollection<Location> LocationRoster { get; } = [];
    public ObservableCollection<ScheduleConflict> CurrentConflicts { get; } = [];
    public ObservableCollection<ScheduleLockChange> ScheduleLockChanges { get; } = [];
    public ObservableCollection<string> RecentFiles { get; } = [];
    public IReadOnlyList<ReportKind> ReportKinds { get; } = Enum.GetValues<ReportKind>();
    public IReadOnlyList<BoneyardSort> BoneyardSorts { get; } = Enum.GetValues<BoneyardSort>();
    public IReadOnlyList<AppLanguage> Languages { get; } = Enum.GetValues<AppLanguage>();
    public IReadOnlyList<CineSchedAppTheme> Themes { get; } = Enum.GetValues<CineSchedAppTheme>();
    public IReadOnlyList<ColorMode> ColorModes { get; } = Enum.GetValues<ColorMode>();

    public event EventHandler<AppSettings>? SettingsChanged;

    public string this[string key] => _settings.GetText(key);

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
    public IRelayCommand SaveProductionCommand { get; }
    public IRelayCommand InitializeCallSheetCommand { get; }
    public IRelayCommand SaveCallSheetCommand { get; }
    public IRelayCommand LockScheduleCommand { get; }
    public IRelayCommand UnlockScheduleCommand { get; }
    public IRelayCommand CopySceneCommand { get; }
    public IAsyncRelayCommand<string> OpenRecentCommand { get; }

    public ShootDay? SelectedDay
    {
        get => _selectedDay;
        set
        {
            if (!SetProperty(ref _selectedDay, value)) return;
            RefreshSelectedDay();
            CallSheetDraft = value?.CallSheet ?? new CallSheetData();
            SendToDayCommand.NotifyCanExecuteChanged();
            InitializeCallSheetCommand.NotifyCanExecuteChanged();
            SaveCallSheetCommand.NotifyCanExecuteChanged();
        }
    }

    public Scene? SelectedBoneyardScene
    {
        get => _selectedBoneyardScene;
        set
        {
            if (!SetProperty(ref _selectedBoneyardScene, value)) return;
            SendToDayCommand.NotifyCanExecuteChanged();
            CopySceneCommand.NotifyCanExecuteChanged();
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

    public BoneyardSort SelectedBoneyardSort
    {
        get => _selectedBoneyardSort;
        set
        {
            if (!SetProperty(ref _selectedBoneyardSort, value)) return;
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

    public bool IsSidebarOpen
    {
        get => !_settings.Current.IsSidebarCollapsed;
        set => ChangeSettings(_settings.Current with { IsSidebarCollapsed = !value });
    }

    public bool IncludeHoldDays
    {
        get => _settings.Current.IncludeHoldDays;
        set => ChangeSettings(_settings.Current with { IncludeHoldDays = value });
    }

    public Brush AccentBrush => new SolidColorBrush(_settings.Current.Theme switch
    {
        CineSchedAppTheme.Green => Windows.UI.Color.FromArgb(255, 22, 163, 74),
        CineSchedAppTheme.Yellow => Windows.UI.Color.FromArgb(255, 202, 138, 4),
        CineSchedAppTheme.System => GetSystemAccentColor(),
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
    public string SidebarLabel => _settings.GetText("settings.sidebar");
    public string HoldDaysLabel => _settings.GetText("settings.holdDays");
    public string ExportLabel => _settings.GetText("reports.export");

    public ProductionInfo ProductionDraft
    {
        get => _productionDraft;
        private set => SetProperty(ref _productionDraft, value);
    }

    public CallSheetData CallSheetDraft
    {
        get => _callSheetDraft;
        private set => SetProperty(ref _callSheetDraft, value);
    }

    public bool IsScheduleLocked => _projects.GetSnapshot().Document.ProductionInfo?.ScheduleLock is not null;

    public void MoveScenesToDay(IReadOnlyList<Guid> sceneIds, ShootDay target)
    {
        if (sceneIds.Count == 0) return;
        SetStatus(_scheduling.MoveScenes(new MoveScenesRequest(sceneIds, null, target.Id, target.Scenes.Count)));
    }

    public void MoveWholeDay(ShootDay source, ShootDay target)
    {
        if (source.Id == target.Id) return;
        SetStatus(_scheduling.MoveWholeDay(new(source.Id, target.Id)));
    }

    public void ChangeDateRange(DateTimeOffset start, DateTimeOffset end, bool shiftExisting) =>
        SetStatus(_scheduling.ChangeDateRange(new(start, end, shiftExisting)));

    public void ReorderScenes(ShootDay day, IReadOnlyList<Guid> sceneIds, int targetIndex) =>
        SetStatus(_scheduling.ReorderScenes(new(day.Id, sceneIds, targetIndex)));

    public void RemoveSceneFromDay(ShootDay day, Scene scene) =>
        SetStatus(_scheduling.RemoveFromDay(day.Id, [scene.Id]));

    public Result<Scene> SaveScene(Scene? existing, SceneInput input)
    {
        var result = existing is null ? _scenes.CreateScene(input) : _scenes.EditScene(existing.Id, input);
        SetStatus(result);
        return result;
    }

    public void DuplicateScene(Scene scene) => SetStatus(_scenes.DuplicateScene(scene.Id));

    public void DeleteScene(Scene scene) => SetStatus(_scenes.DeleteScene(scene.Id));

    public IReadOnlyList<Scene> GetScriptOrder() => _scenes.GetScriptOrder();

    public void AddBanner(BannerType type, string title, string note, int duration, string color, string startTime)
    {
        if (SelectedDay is null) return;
        SetStatus(_stripboard.AddBanner(SelectedDay.Id, type, title, note, duration, color, startTime));
    }

    public void AddMeal(MealKind kind, string startTime)
    {
        if (SelectedDay is null) return;
        SetStatus(_stripboard.AddMeal(SelectedDay.Id, kind, startTime));
    }

    public void AddCalendarEvent(string title, string startTime, string color, int duration)
    {
        if (SelectedDay is null) return;
        SetStatus(_stripboard.AddCalendarEvent(SelectedDay.Id, title, startTime, color, duration));
    }

    public void ToggleBlackout(ShootDay day, bool matchingWeekday = false) =>
        SetStatus(_scheduling.SetBlackout(new(day.Id, !day.IsBlackout, matchingWeekday)));

    public CastMember AddCast(string actor, string character) => _production.AddCast(actor, character);
    public CrewMember AddCrew(string name, string role, string phone, bool daily) => _production.AddCrew(name, role, phone, daily);
    public Location AddLocation(string name, string address) => _production.AddLocation(name, address);
    public void UpdateCast(CastMember member, string actor, string character) =>
        SetStatus(_production.UpdateCast(member.Id, actor, character));
    public void UpdateCrew(CrewMember member, string name, string role, string phone, bool daily) =>
        SetStatus(_production.UpdateCrew(member.Id, name, role, phone, daily));
    public void UpdateLocation(Location location, string name, string address) =>
        SetStatus(_production.UpdateLocation(location.Id, name, address));
    public void RemoveCast(CastMember member) => SetStatus(_production.RemoveCast(member.Id));
    public void RemoveCrew(CrewMember member) => SetStatus(_production.RemoveCrew(member.Id));
    public void RemoveLocation(Location location) => SetStatus(_production.RemoveLocation(location.Id));
    public Result<DateRange> AddUnavailableRange(CastMember member, DateTimeOffset start, DateTimeOffset end)
    {
        var result = _production.AddUnavailableRange(member.Id, start, end);
        SetStatus(result);
        return result;
    }

    public void RemoveUnavailableRange(CastMember member, DateRange range) =>
        SetStatus(_production.RemoveUnavailableRange(member.Id, range.Id));

    public void SetCallSheetCrew(IEnumerable<Guid> crewIds)
    {
        CallSheetDraft.CrewIDOverride = crewIds.Distinct().ToList();
        OnPropertyChanged(nameof(CallSheetDraft));
        Status = "Call sheet crew selection updated; save to commit";
    }

    private async Task NewProjectAsync()
    {
        if (_projects.IsDirty && (!await _messages.ConfirmAsync(
                "Create a new project?", "Unsaved changes will remain only in the recovery autosave.", "Create") ||
            !await PreserveUnsavedSnapshotAsync()))
        {
            return;
        }

        _lifecycle.StartNewProject(DateTimeOffset.Now);
        Status = "New project created";
    }

    private async Task OpenProjectAsync()
    {
        if (_projects.IsDirty && (!await _messages.ConfirmAsync(
                "Open another project?", "The current project has unsaved changes.", "Open") ||
            !await PreserveUnsavedSnapshotAsync()))
        {
            return;
        }

        var result = await _lifecycle.OpenFromPickerAsync();
        if (result.IsSuccess && result.Value)
        {
            Status = $"Opened {_lifecycle.AssociatedFile?.Name}";
            RefreshRecentFiles();
        }
        else if (!result.IsSuccess) Status = result.Error!.Message;
    }

    private async Task SaveProjectAsync()
    {
        var result = await _lifecycle.SaveAsync();
        if (result.IsSuccess && result.Value)
        {
            Status = $"Saved {_lifecycle.AssociatedFile?.Name}";
            RefreshRecentFiles();
        }
        else if (!result.IsSuccess)
        {
            Status = result.Error!.Message;
        }
    }

    private async Task SaveAsProjectAsync()
    {
        var result = await _lifecycle.SaveAsAsync();
        if (result.IsSuccess && result.Value)
        {
            Status = $"Saved {_lifecycle.AssociatedFile?.Name}";
            RefreshRecentFiles();
        }
        else if (!result.IsSuccess)
        {
            Status = result.Error!.Message;
        }
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

    private async Task OpenRecentAsync(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return;
        if (_projects.IsDirty && (!await _messages.ConfirmAsync(
                "Open recent project?", "The current project has unsaved changes.", "Open") ||
            !await PreserveUnsavedSnapshotAsync())) return;
        var result = await _lifecycle.OpenRecentAsync(identity);
        if (result.IsSuccess && result.Value) Status = $"Opened {_lifecycle.AssociatedFile?.Name}";
        else if (!result.IsSuccess) Status = result.Error!.Message;
        RefreshRecentFiles();
    }

    private async Task<bool> PreserveUnsavedSnapshotAsync()
    {
        var result = await _autosave.SaveNowAsync(_projects.GetSnapshot(), markRecoverable: true);
        if (result.IsSuccess) return true;
        Status = $"Could not preserve unsaved changes: {result.Error!.Message}";
        return false;
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
        RefreshProductionAndDiagnostics(document);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectedDay() => Replace(SelectedDayScenes, SelectedDay?.Scenes ?? []);

    private void RefreshProductionAndDiagnostics(ProjectDocument document)
    {
        ProductionDraft = document.ProductionInfo ?? new ProductionInfo();
        Replace(CastRoster, ProductionDraft.CastList);
        Replace(CrewRoster, ProductionDraft.Crew);
        Replace(LocationRoster, ProductionDraft.LocationRoster);
        CallSheetDraft = SelectedDay?.CallSheet ?? new CallSheetData();
        Replace(CurrentConflicts, _conflicts.ScanAvailability().Concat(_conflicts.ScanBlackouts()));
        Replace(ScheduleLockChanges, _scheduleLock.GetChanges());
        OnPropertyChanged(nameof(IsScheduleLocked));
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles() => Replace(RecentFiles, _recentFiles.Get());

    private void RefreshBoneyard()
    {
        var values = string.IsNullOrWhiteSpace(SearchQuery)
            ? _scenes.SortBoneyard(SelectedBoneyardSort)
            : _scenes.SearchScenes(SearchQuery).Select(result => result.Scene)
                .GroupBy(scene => scene.Id).Select(group => group.First()).ToList();
        Replace(Boneyard, values);
    }

    private void SetStatus<T>(Result<T> result) => Status = result.IsSuccess ? "Done" : result.Error!.Message;

    private void SaveProduction()
    {
        var draft = ProductionDraft;
        _production.Update(target =>
        {
            target.CompanyName = draft.CompanyName;
            target.DirectorName = draft.DirectorName;
            target.DirectorPhone = draft.DirectorPhone;
            target.ProducerName = draft.ProducerName;
            target.ProducerPhone = draft.ProducerPhone;
            target.AdName = draft.AdName;
            target.AdPhone = draft.AdPhone;
            target.ContactNumber = draft.ContactNumber;
            target.DefaultLunchTime = draft.DefaultLunchTime;
        });
        Status = "Production setup saved";
    }

    private void InitializeCallSheet()
    {
        if (SelectedDay is null) return;
        SetStatus(_callSheets.Initialize(SelectedDay.Id));
    }

    private void SaveCallSheet()
    {
        if (SelectedDay is null) return;
        var draft = CallSheetDraft;
        SetStatus(_callSheets.Update(SelectedDay.Id, target => CopyCallSheet(draft, target)));
    }

    private void LockSchedule()
    {
        _scheduleLock.Lock(DateTimeOffset.Now);
        Status = "Schedule baseline captured";
    }

    private void UnlockSchedule()
    {
        _scheduleLock.Unlock();
        Status = "Schedule baseline removed";
    }

    private void CopySelectedScene()
    {
        if (SelectedBoneyardScene is null) return;
        SetStatus(_clipboard.CopyText($"{SelectedBoneyardScene.DisplayTitle}\n{SelectedBoneyardScene.Summary}"));
    }

    private static void CopyCallSheet(CallSheetData source, CallSheetData target)
    {
        target.GeneralCallTime = source.GeneralCallTime;
        target.WorkDaySchedule = source.WorkDaySchedule;
        target.ReadyToShootTime = source.ReadyToShootTime;
        target.LunchTime = source.LunchTime;
        target.SnackTime = source.SnackTime;
        target.DinnerTime = source.DinnerTime;
        target.WrapTime = source.WrapTime;
        target.QuoteOfTheDay = source.QuoteOfTheDay;
        target.ProdManagerContact = source.ProdManagerContact;
        target.AdContact = source.AdContact;
        target.WeatherTemp = source.WeatherTemp;
        target.WeatherCondition = source.WeatherCondition;
        target.WeatherPrecipWind = source.WeatherPrecipWind;
        target.SunTimes = source.SunTimes;
        target.BasecampLocation = source.BasecampLocation;
        target.NearestHospital = source.NearestHospital;
        target.CastCallEntries = [.. source.CastCallEntries];
        target.CrewCallEntries = [.. source.CrewCallEntries];
        target.ProductionNotes = [.. source.ProductionNotes];
        target.Locations = [.. source.Locations];
        target.CastOverride = source.CastOverride is null ? null : [.. source.CastOverride];
        target.CrewOverride = source.CrewOverride is null ? null : [.. source.CrewOverride];
        target.CrewIDOverride = source.CrewIDOverride is null ? null : [.. source.CrewIDOverride];
        target.CrewOneOffs = source.CrewOneOffs is null ? null : [.. source.CrewOneOffs];
        target.Notes = source.Notes;
    }

    private void ChangeSettings(AppSettings settings)
    {
        if (settings == _settings.Current) return;
        var languageChanged = settings.Language != _settings.Current.Language;
        _settings.Update(settings);
        _ = _preferences.SetAsync("app-settings", settings);
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedColorMode));
        OnPropertyChanged(nameof(IsSidebarOpen));
        OnPropertyChanged(nameof(IncludeHoldDays));
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(AccentBrush));
        foreach (var property in new[]
                 {
                     nameof(CalendarLabel), nameof(StripboardLabel), nameof(ProductionLabel), nameof(ReportsLabel),
                     nameof(SettingsLabel), nameof(NewLabel), nameof(OpenLabel), nameof(SaveLabel), nameof(SaveAsLabel),
                     nameof(ImportLabel), nameof(UndoLabel), nameof(RedoLabel), nameof(LanguageLabel), nameof(ThemeLabel),
                     nameof(ColorModeLabel), nameof(SidebarLabel), nameof(HoldDaysLabel), nameof(ExportLabel)
                 })
            OnPropertyChanged(property);
        if (languageChanged) Refresh();
        SettingsChanged?.Invoke(this, settings);
    }

    private static Windows.UI.Color GetSystemAccentColor()
    {
        var resources = Application.Current?.Resources;
        return resources is not null && resources.ContainsKey("SystemAccentColor") &&
               resources["SystemAccentColor"] is Windows.UI.Color color
            ? color
            : Windows.UI.Color.FromArgb(255, 37, 99, 235);
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
