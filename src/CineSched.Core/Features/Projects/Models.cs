using CineSched.Core.Features.Production;
using CineSched.Core.Features.Scenes;
using CineSched.Core.Features.Scheduling;

namespace CineSched.Core.Features.Projects;

public sealed class ProjectDocument
{
    public List<Scene> AllScenes { get; set; } = [];
    public List<ShootDay> ShootDays { get; set; } = [];
    public string ProjectTitle { get; set; } = "Untitled Movie";
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    public bool? IsShiftModeEnabled { get; set; } = false;
    public ProductionInfo? ProductionInfo { get; set; } = new();
}

public sealed record ProjectChangedEvent(long Revision, bool IsDirty, string ChangeKind);

public sealed record ProjectSnapshot(ProjectDocument Document, long Revision);
