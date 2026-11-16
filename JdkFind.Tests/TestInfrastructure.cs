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
        catch (UnauthorizedAccessException) { }
    }
}

internal static class TestJdk
{
    /// <summary>Creates a minimal fake JDK home: <c>bin/java</c> plus a <c>release</c> file.</summary>
    internal static string Create(string root, string javaVersion, params string[] segments) =>
        CreateWithImplementor(root, javaVersion, "Test Vendor", segments);

    /// <summary>
    ///     Same as <see cref="Create" />, but the fake java executable sleeps before
    ///     answering — for tests that need a probe slow enough to observe. POSIX only.
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

    /// <summary>
    ///     Creates a home whose java waits for a peer probe's marker before finishing,
    ///     and records that it observed the peer. Two probes running concurrently
    ///     therefore leave both overlap markers; serialized probes leave only the
    ///     second — no wall-clock threshold is involved. POSIX only.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    internal static string CreateRendezvousProbe(string root, string javaVersion, string rendezvousDirectory,
        string id, string peerId, params string[] segments)
    {
        var home = Create(root, javaVersion, segments);
        var java = Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName);
        var started = Path.Combine(rendezvousDirectory, id);
        var peer = Path.Combine(rendezvousDirectory, peerId);
        var overlap = Path.Combine(rendezvousDirectory, id + ".overlap");
        File.WriteAllText(java, $"""
                                  #!/bin/sh
                                  touch "{started}"
                                  i=0
                                  while [ ! -f "{peer}" ] && [ "$i" -lt 200 ]; do
                                      sleep 0.05
                                      i=$((i + 1))
                                  done
                                  [ -f "{peer}" ] && touch "{overlap}"
                                  echo "    java.version = {javaVersion}" >&2
                                  """);
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return home;
    }

    /// <summary>
    ///     Creates a standalone POSIX <c>java</c> script (no home layout and no
    ///     release file) that reports <paramref name="javaVersion" /> to stderr.
    ///     <paramref name="slow" /> delays the report, for ordering tests.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    internal static string CreateStandaloneJava(string directory, string javaVersion, bool slow = false)
    {
        Directory.CreateDirectory(directory);
        var java = Path.Combine(directory, JavaHomeLayout.JavaExecutableName);
        var prelude = slow ? "sleep 0.2" : ":";
        File.WriteAllText(java, $"""
                                  #!/bin/sh
                                  {prelude}
                                  cat >&2 <<'EOPROBE'
                                      java.version = {javaVersion}
                                      java.vendor = Test Vendor
                                  EOPROBE
                                  """);
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return java;
    }

    /// <summary>Same as <see cref="Create" />, but controls the <c>IMPLEMENTOR</c> value.</summary>
    internal static string CreateWithImplementor(string root, string javaVersion, string implementor,
        params string[] segments)
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
