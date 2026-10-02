using System.Runtime.Versioning;

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
    internal static string Create(string root, string javaVersion, params string[] segments) =>
        CreateWithImplementor(root, javaVersion, "Test Vendor", segments);

    /// <summary>
    ///     Same as <see cref="Create" />, but the fake java executable sleeps before
    ///     answering — for asserting concurrent probing behavior. POSIX only.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    internal static string CreateSlowProbe(string root, string javaVersion, params string[] segments)
    {
        var home = Create(root, javaVersion, segments);
        var java = Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, """
            #!/bin/sh
            sleep 1
            echo "    java.version = 99.0" >&2
            """);
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return home;
    }

    /// <summary>Same as <see cref="Create" />, but controls the <c>IMPLEMENTOR</c> value.</summary>
    internal static string CreateWithImplementor(string root, string javaVersion, string implementor, params string[] segments)
    {
        var home = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        File.WriteAllText(
            Path.Combine(home, "release"),
            $"""
             JAVA_VERSION="{javaVersion}"
             IMPLEMENTOR="{implementor}"
             OS_ARCH="aarch64"
             """);
        return home;
    }

    internal static List<string> GetHomes(this IJvmProvider provider)
    {
        var homes = new List<string>();
        foreach (var home in provider.GetJavaHomes())
            homes.Add(home);

        return homes;
    }
}
