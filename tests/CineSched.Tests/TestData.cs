namespace CineSched.Tests;

internal static class TestData
{
    public static string Asset(params string[] segments) =>
        Path.Combine([AppContext.BaseDirectory, "TestAssets", .. segments]);

    public static ProjectService Project(DateTimeOffset? start = null, int days = 3)
    {
        var projects = new ProjectService();
        var first = start ?? new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        projects.NewProject(first, first, first.AddDays(days - 1));
        return projects;
    }

    public static Scene Scene(string number, string title = "INT. SET - DAY", params string[] cast) => new()
    {
        Id = Guid.NewGuid(),
        SceneNumber = number,
        Title = title,
        Cast = [.. cast],
        Duration = 8,
        EstimatedTime = 30
    };
}
