namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

public static class FixtureLoader
{
    private const string ParentDirectory = "..";
    private const string FixturesDirectory = "Fixtures";

    public static string Read(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            ParentDirectory,
            ParentDirectory,
            ParentDirectory,
            FixturesDirectory,
            fileName));

        return File.ReadAllText(path);
    }
}
