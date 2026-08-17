using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CineSched.Core.Common;
using CineSched.Core.Features.CallSheets;
using CineSched.Core.Features.Production;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.Scheduling;

namespace CineSched.Core.Features.Projects;

public sealed class ProjectService
{
    private const int MaxUndoDepth = 30;
    private static readonly DateTimeOffset AppleReferenceDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Lock _gate = new();
    private readonly List<ProjectDocument> _undo = [];
    private readonly List<ProjectDocument> _redo = [];
    private readonly JsonSerializerOptions _json;

    public ProjectService()
    {
        _json = CreateJsonOptions();
        CurrentDocument = CreateNewDocument(DateTimeOffset.Now);
    }

    public event EventHandler<ProjectChangedEvent>? Changed;

    public ProjectDocument CurrentDocument { get; private set; }

    public bool IsDirty { get; private set; }

    public long Revision { get; private set; }

    public bool CanUndo
    {
        get { lock (_gate) return _undo.Count > 0; }
    }

    public bool CanRedo
    {
        get { lock (_gate) return _redo.Count > 0; }
    }

    public ProjectDocument NewProject(DateTimeOffset now, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var document = CreateNewDocument(now, start, end);
        lock (_gate)
        {
            _undo.Clear();
            _redo.Clear();
            CurrentDocument = document;
            IsDirty = false;
            Revision++;
        }

        RaiseChanged("project.new");
        return Snapshot();
    }

    public async ValueTask<Result<ProjectDocument>> OpenAsync(Stream source, CancellationToken cancellationToken = default)
    {
        try
        {
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            var bytes = memory.ToArray();
            using var root = JsonDocument.Parse(bytes);
            var isLegacy = !root.RootElement.TryGetProperty("projectTitle", out _);

            var document = JsonSerializer.Deserialize<ProjectDocument>(bytes, _json);
            if (document is null)
            {
                return Result<ProjectDocument>.Failure("project.invalid-json", "The project document is empty or invalid.");
            }

            Normalize(document, isLegacy);

            lock (_gate)
            {
                _undo.Clear();
                _redo.Clear();
                CurrentDocument = document;
                IsDirty = false;
                Revision++;
            }

            RaiseChanged("project.open");
            return Result<ProjectDocument>.Success(Snapshot());
        }
        catch (JsonException exception)
        {
            return Result<ProjectDocument>.Failure(
                "project.invalid-json",
                exception.Message,
                new Dictionary<string, object?> { ["exception"] = exception.GetType().Name });
        }
        catch (NotSupportedException exception)
        {
            return Result<ProjectDocument>.Failure(
                "project.unsupported-data",
                exception.Message,
                new Dictionary<string, object?> { ["exception"] = exception.GetType().Name });
        }
        catch (IOException exception)
        {
            return Result<ProjectDocument>.Failure(
                "project.read-failed",
                exception.Message,
                new Dictionary<string, object?> { ["exception"] = exception.GetType().Name });
        }
    }

    public async ValueTask<Result<Unit>> SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        var snapshot = Snapshot();
        var result = await WriteAsync(snapshot, destination, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        lock (_gate)
        {
            IsDirty = false;
        }

        RaiseChanged("project.saved");
        return result;
    }

    public async ValueTask<Result<Unit>> WriteAsync(
        ProjectDocument document,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await JsonSerializer.SerializeAsync(destination, document, _json, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or JsonException)
        {
            return Result<Unit>.Failure(
                "project.write-failed",
                exception.Message,
                new Dictionary<string, object?> { ["exception"] = exception.GetType().Name });
        }
    }

    public ProjectSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new ProjectSnapshot(Clone(CurrentDocument), Revision);
        }
    }

    public ProjectDocument Update(Action<ProjectDocument> edit, string changeKind = "project.updated", bool structural = false)
    {
        var document = Snapshot();
        edit(document);
        return Apply(document, changeKind, structural);
    }

    internal ProjectDocument Snapshot()
    {
        lock (_gate)
        {
            return Clone(CurrentDocument);
        }
    }

    internal ProjectDocument Apply(ProjectDocument document, string changeKind, bool structural = true)
    {
        lock (_gate)
        {
            if (structural)
            {
                _undo.Add(Clone(CurrentDocument));
                if (_undo.Count > MaxUndoDepth)
                {
                    _undo.RemoveAt(0);
                }

                _redo.Clear();
            }

            CurrentDocument = Clone(document);
            IsDirty = true;
            Revision++;
        }

        RaiseChanged(changeKind);
        return Snapshot();
    }

    internal Result<ProjectDocument> Undo()
    {
        lock (_gate)
        {
            if (_undo.Count == 0)
            {
                return Result<ProjectDocument>.Failure("project.nothing-to-undo", "There is no operation to undo.");
            }

            _redo.Add(Clone(CurrentDocument));
            CurrentDocument = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            IsDirty = true;
            Revision++;
        }

        RaiseChanged("project.undo");
        return Result<ProjectDocument>.Success(Snapshot());
    }

    internal Result<ProjectDocument> Redo()
    {
        lock (_gate)
        {
            if (_redo.Count == 0)
            {
                return Result<ProjectDocument>.Failure("project.nothing-to-redo", "There is no operation to redo.");
            }

            _undo.Add(Clone(CurrentDocument));
            CurrentDocument = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            IsDirty = true;
            Revision++;
        }

        RaiseChanged("project.redo");
        return Result<ProjectDocument>.Success(Snapshot());
    }

    internal ProjectDocument Clone(ProjectDocument document)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(document, _json);
        return JsonSerializer.Deserialize<ProjectDocument>(json, _json)
            ?? throw new InvalidOperationException("Unable to clone the project document.");
    }

    private static ProjectDocument CreateNewDocument(
        DateTimeOffset now,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null)
    {
        var first = StartOfDay(start ?? now);
        var last = StartOfDay(end ?? first.AddDays(30));
        if (last < first)
        {
            last = first;
        }

        var days = new List<ShootDay>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            days.Add(new ShootDay { Date = date });
        }

        return new ProjectDocument
        {
            ProjectTitle = "Untitled Movie",
            CreatedDate = now,
            IsShiftModeEnabled = false,
            ProductionInfo = new ProductionInfo(),
            ShootDays = days
        };
    }

    private static DateTimeOffset StartOfDay(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

    private void RaiseChanged(string kind)
    {
        ProjectChangedEvent value;
        lock (_gate)
        {
            value = new ProjectChangedEvent(Revision, IsDirty, kind);
        }

        Changed?.Invoke(this, value);
    }

    private static void Normalize(ProjectDocument document, bool isLegacy)
    {
        document.AllScenes ??= [];
        document.ShootDays ??= [];
        document.ProjectTitle = isLegacy
            ? "Imported Project"
            : string.IsNullOrWhiteSpace(document.ProjectTitle) ? "Untitled Movie" : document.ProjectTitle;
        document.ProductionInfo ??= new ProductionInfo();
        Normalize(document.ProductionInfo);

        foreach (var scene in document.AllScenes)
        {
            Normalize(scene);
        }

        foreach (var day in document.ShootDays)
        {
            day.Scenes ??= [];
            day.CallSheet ??= new CallSheetData();
            Normalize(day.CallSheet);
            foreach (var scene in day.Scenes)
            {
                Normalize(scene);
            }
        }
    }

    private static void Normalize(Scene scene)
    {
        scene.Title ??= string.Empty;
        scene.SceneNumber ??= string.Empty;
        scene.Cast ??= [];
        scene.Summary ??= string.Empty;
        scene.RealLocation ??= string.Empty;
        scene.LocationAddress ??= string.Empty;
        scene.Extras ??= [];
        scene.Props ??= [];
        scene.SetDressing ??= [];
        scene.Wardrobe ??= [];
        scene.MakeupHair ??= [];
        scene.Vehicles ??= [];
        scene.SpecialEquipment ??= [];
        scene.Stunts ??= [];
        scene.Sfx ??= [];
        scene.Vfx ??= [];
        scene.BreakdownNotes ??= string.Empty;
        scene.BannerTitle ??= string.Empty;
        scene.BannerNote ??= string.Empty;
        scene.BannerColorHex ??= string.Empty;
        scene.CustomStartTime ??= string.Empty;
    }

    private static void Normalize(ProductionInfo production)
    {
        production.CompanyName ??= string.Empty;
        production.DirectorName ??= string.Empty;
        production.DirectorPhone ??= string.Empty;
        production.ProducerName ??= string.Empty;
        production.ProducerPhone ??= string.Empty;
        production.AdName ??= string.Empty;
        production.AdPhone ??= string.Empty;
        production.ContactNumber ??= string.Empty;
        production.DefaultLunchTime ??= "01:30 PM";
        production.Crew ??= [];
        production.CastList ??= [];
        production.LocationRoster ??= [];
        foreach (var member in production.CastList)
        {
            member.UnavailableRanges ??= [];
        }
    }

    private static void Normalize(CallSheetData callSheet)
    {
        callSheet.CastCallEntries ??= [];
        callSheet.CrewCallEntries ??= [];
        callSheet.ProductionNotes ??= [];
        callSheet.Locations ??= [];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new SwiftDateTimeOffsetConverter());
        options.Converters.Add(new FlexibleStringListConverter());
        return options;
    }

    private sealed class SwiftDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var seconds))
            {
                return AppleReferenceDate.AddSeconds(seconds);
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected a Swift date string or numeric reference-date value.");
            }

            var raw = reader.GetString() ?? throw new JsonException("Date value cannot be null.");
            if (raw.Length >= 5 && (raw[^5] == '+' || raw[^5] == '-') && raw[^3] != ':')
            {
                raw = raw.Insert(raw.Length - 2, ":");
            }

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var value))
            {
                return value;
            }

            throw new JsonException($"Invalid Swift date value '{raw}'.");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            var offset = value.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty, StringComparison.Ordinal);
            writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + offset);
        }
    }

    private sealed class FlexibleStringListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return (reader.GetString() ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(item => item.Length > 0)
                    .ToList();
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected a string or string array.");
            }

            var values = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    values.Add(reader.GetString() ?? string.Empty);
                }
                else if (reader.TokenType != JsonTokenType.Null)
                {
                    throw new JsonException("String list contains a non-string value.");
                }
            }

            return values;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
    }
}
