using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CineSched.Core.Common;
using CineSched.Core.Features.Projects;
using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.ScriptImport;

public sealed partial class ScriptImportService(ProjectService projects)
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".fdx", ".xml", ".fountain", ".md", ".spmd", ".highland" };

    public bool Supports(string extension) => SupportedExtensions.Contains(NormalizeExtension(extension));

    public async ValueTask<Result<ScriptImportResult>> ImportAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default)
    {
        extension = NormalizeExtension(extension);
        if (!Supports(extension))
        {
            return Result<ScriptImportResult>.Failure("import.unsupported-extension", $"The extension '{extension}' is not supported.");
        }

        try
        {
            var result = extension switch
            {
                ".fdx" or ".xml" => await ImportFinalDraftAsync(source, extension, cancellationToken).ConfigureAwait(false),
                ".highland" => await ImportHighlandAsync(source, cancellationToken).ConfigureAwait(false),
                _ => await ImportFountainAsync(source, extension, cancellationToken).ConfigureAwait(false)
            };

            if (result.Scenes.Count == 0)
            {
                return Result<ScriptImportResult>.Failure("import.no-scenes", "No scene headings were found in the script.");
            }

            return Result<ScriptImportResult>.Success(result);
        }
        catch (InvalidDataException exception)
        {
            return Result<ScriptImportResult>.Failure(
                extension == ".highland" ? "import.invalid-highland" : "import.invalid-fdx",
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return Result<ScriptImportResult>.Failure("import.read-failed", exception.Message);
        }
    }

    public Result<int> Commit(ScriptImportResult import)
    {
        if (import.Scenes.Count == 0)
        {
            return Result<int>.Failure("import.no-scenes", "There are no parsed scenes to import.");
        }

        var document = projects.Snapshot();
        var existingIds = document.AllScenes.Concat(document.ShootDays.SelectMany(day => day.Scenes))
            .Select(scene => scene.Id).ToHashSet();
        var additions = import.Scenes.Select(CloneForImport).ToList();
        foreach (var scene in additions.Where(scene => existingIds.Contains(scene.Id)))
        {
            scene.Id = Guid.NewGuid();
        }

        document.AllScenes.AddRange(additions);
        projects.Apply(document, "script.imported");
        return Result<int>.Success(additions.Count);
    }

    private static async ValueTask<ScriptImportResult> ImportFinalDraftAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(source, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        var paragraphs = document.Descendants().Where(element => element.Name.LocalName == "Paragraph").ToList();
        var scenes = new List<Scene>();
        Scene? current = null;

        foreach (var paragraph in paragraphs)
        {
            var type = paragraph.Attribute("Type")?.Value ?? string.Empty;
            var text = string.Concat(paragraph.DescendantsAndSelf()
                .Where(element => element.Name.LocalName is "Text" or "Paragraph")
                .Where(element => !element.HasElements || element.Name.LocalName == "Text")
                .Select(element => element.Value)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (type.Equals("Scene Heading", StringComparison.OrdinalIgnoreCase))
            {
                current = CreateScene(text, paragraph.Attribute("Number")?.Value);
                scenes.Add(current);
            }
            else if (current is not null && type.Equals("Character", StringComparison.OrdinalIgnoreCase))
            {
                var character = NormalizeCharacter(text);
                if (character.Length > 0 && !current.Cast.Contains(character, StringComparer.OrdinalIgnoreCase))
                {
                    current.Cast.Add(character);
                }
            }
            else if (current is not null && type.Equals("Action", StringComparison.OrdinalIgnoreCase))
            {
                current.Summary = string.IsNullOrEmpty(current.Summary) ? text : $"{current.Summary} {text}";
            }
        }

        return new ScriptImportResult(scenes, extension, scenes.Count);
    }

    private static async ValueTask<ScriptImportResult> ImportFountainAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return ParseFountain(text, sourceName);
    }

    private static async ValueTask<ScriptImportResult> ImportHighlandAsync(Stream source, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var preferred = new[] { "text.fountain", "text.markdown", "text.txt" };
        var entry = preferred.Select(name => archive.Entries.FirstOrDefault(candidate =>
                candidate.FullName.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(candidate => candidate is not null);
        if (entry is null)
        {
            throw new InvalidDataException("The Highland archive does not contain text.fountain, text.markdown, or text.txt.");
        }

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return ParseFountain(text, ".highland");
    }

    private static ScriptImportResult ParseFountain(string text, string sourceName)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var scenes = new List<Scene>();
        Scene? current = null;
        var pageCount = 1;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line == "===")
            {
                pageCount++;
                continue;
            }

            if (IsSceneHeading(line))
            {
                current = CreateScene(line.TrimStart('.'), null);
                scenes.Add(current);
                continue;
            }

            if (current is null || line.Length == 0)
            {
                continue;
            }

            if (LooksLikeCharacter(lines, index, line))
            {
                var character = NormalizeCharacter(line.TrimStart('@'));
                if (character.Length > 0 && !current.Cast.Contains(character, StringComparer.OrdinalIgnoreCase))
                {
                    current.Cast.Add(character);
                }
            }
            else if (!line.StartsWith('>') && current.Summary.Length < 500)
            {
                current.Summary = string.IsNullOrEmpty(current.Summary) ? line : $"{current.Summary} {line}";
            }
        }

        return new ScriptImportResult(scenes, sourceName, pageCount);
    }

    private static Scene CreateScene(string heading, string? explicitNumber)
    {
        heading = heading.Trim();
        var trailingNumber = TrailingSceneNumberRegex().Match(heading);
        var sceneNumber = explicitNumber?.Trim() ?? string.Empty;
        if (trailingNumber.Success)
        {
            sceneNumber = trailingNumber.Groups["number"].Value;
            heading = heading[..trailingNumber.Index].Trim();
        }

        if (string.IsNullOrEmpty(sceneNumber))
        {
            var leading = LeadingSceneNumberRegex().Match(heading);
            if (leading.Success)
            {
                sceneNumber = leading.Groups["number"].Value;
                heading = heading[leading.Length..].Trim();
            }
        }

        var upper = heading.ToUpperInvariant();
        return new Scene
        {
            SceneNumber = sceneNumber,
            Title = upper,
            DayNightType = ParseDayNight(upper),
            Duration = 1,
            EstimatedTime = 15
        };
    }

    private static bool IsSceneHeading(string line)
    {
        var candidate = line.TrimStart('.').TrimStart();
        return SceneHeadingRegex().IsMatch(candidate);
    }

    private static bool LooksLikeCharacter(string[] lines, int index, string line)
    {
        if (line.StartsWith('@'))
        {
            return true;
        }

        if (line.Length > 60 || line != line.ToUpperInvariant() || !line.Any(char.IsLetter))
        {
            return false;
        }

        return index + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[index + 1]);
    }

    private static string NormalizeCharacter(string text) =>
        ParentheticalRegex().Replace(text, string.Empty).Trim().ToUpperInvariant();

    private static DayNightType ParseDayNight(string heading)
    {
        if (heading.Contains("DAWN", StringComparison.OrdinalIgnoreCase)) return DayNightType.Dawn;
        if (heading.Contains("DUSK", StringComparison.OrdinalIgnoreCase)) return DayNightType.Dusk;
        if (heading.Contains("AFTERNOON", StringComparison.OrdinalIgnoreCase)) return DayNightType.Afternoon;
        if (heading.Contains("NIGHT", StringComparison.OrdinalIgnoreCase)) return DayNightType.Night;
        return DayNightType.Day;
    }

    private static string NormalizeExtension(string extension)
    {
        var value = extension.Trim();
        return value.StartsWith('.') ? value.ToLowerInvariant() : $".{value.ToLowerInvariant()}";
    }

    private static Scene CloneForImport(Scene scene)
    {
        scene.Cast = [.. scene.Cast];
        return scene;
    }

    [GeneratedRegex(@"^(?:INT\.?|EXT\.?|INT\.?/EXT\.?|I\.?/E\.?|EST\.)(?:\s|\.)", RegexOptions.IgnoreCase)]
    private static partial Regex SceneHeadingRegex();

    [GeneratedRegex(@"^#(?<number>[^#]+)#\s*$")]
    private static partial Regex OnlySceneNumberRegex();

    [GeneratedRegex(@"\s+#(?<number>[^#]+)#\s*$")]
    private static partial Regex TrailingSceneNumberRegex();

    [GeneratedRegex(@"^(?<number>\d+[A-Za-z]?)\.\s+")]
    private static partial Regex LeadingSceneNumberRegex();

    [GeneratedRegex(@"\s*\([^)]*\)\s*$")]
    private static partial Regex ParentheticalRegex();
}
