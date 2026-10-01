namespace JdkFind.Tests;

internal sealed class TempDirectory : IDisposable
{
    internal string FullPath { get; } =
        Path.Combine(Path.GetTempPath(), "jdkfind-tests", Guid.NewGuid().ToString("N"));

    internal TempDirectory() => Directory.CreateDirectory(FullPath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, true);
        }
        catch (IOException)
        {
            // A cleanup failure is not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class TestJdk
{
    /// <summary>Creates a minimal fake JDK home: <c>bin/java</c> plus a <c>release</c> file.</summary>
    internal static string Create(string root, string javaVersion, params string[] segments)
    {
        var home = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        File.WriteAllText(
            Path.Combine(home, "release"),
            $"""
             JAVA_VERSION="{javaVersion}"
             IMPLEMENTOR="Test Vendor"
             OS_ARCH="aarch64"
             """);
        return home;
    }

    internal static async Task<List<string>> GetHomesAsync(this IJvmProvider provider)
    {
        var homes = new List<string>();
        await foreach (var home in provider.GetJavaHomesAsync())
            homes.Add(home);

        return homes;
    }
}
