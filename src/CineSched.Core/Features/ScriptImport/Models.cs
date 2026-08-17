using CineSched.Core.Features.Scenes;

namespace CineSched.Core.Features.ScriptImport;

public sealed record ScriptImportResult(
    IReadOnlyList<Scene> Scenes,
    string SourceName,
    int PageCount = 0,
    IReadOnlyList<string>? Warnings = null);
