namespace JobFetchProbe.Tests;

internal static class TestPaths
{
    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jfp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
