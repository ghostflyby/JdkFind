namespace JdkFind.Tests;

/// <summary>
///     Cancellation-contract tests for the async locate pipeline: a cancelled token
///     aborts before any provider work, probe or release read, and the async results
///     match the synchronous ones.
/// </summary>
public class JdkFinderAsyncTests : IDisposable
{
    private readonly TempDirectory temp = new();

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    private JdkFindOptions StubOptions(params string[] homes) =>
        new() { Providers = [new StubJvmProvider("stub", homes)], ProbeRuntimeProperties = false };

    private static CancellationToken Cancelled => new(canceled: true);

    [Fact]
    public async Task LocateAsync_MatchesTheSyncPipeline()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");
        var options = StubOptions(jdk);

        var located = await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken);
        var sync = JdkFinder.Locate(options).ToList();

        Assert.Single(located);
        Assert.Equal(sync[0].Home.FullName, located[0].Home.FullName);
        Assert.Equal(sync[0].Providers, located[0].Providers);
    }

    [Fact]
    public async Task LocateAsync_PreCancelledToken_ThrowsBeforeAnyWork()
    {
        var options = StubOptions("/nonexistent/home");

        await Assert.ThrowsAsync<OperationCanceledException>(() => JdkFinder.LocateAsync(options, Cancelled));
    }

    [Fact]
    public async Task ProbeAsync_PreCancelledToken_Throws()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        await Assert.ThrowsAsync<OperationCanceledException>(() => JvmRuntimeProbe.ProbeAsync(jdk, Cancelled));
    }

    [Fact]
    public async Task LocateAsync_HonorsProbeRuntimeProperties()
    {
        // A working java stub makes probing observable: enabled, the runtime enriches
        // from the stub's output; disabled, it stays null. Windows is skipped — the
        // stub needs a POSIX script interpreter.
        if (OperatingSystem.IsWindows())
            return;

        var home = Path.Combine(temp.FullPath, "scripted");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "release"), "JAVA_VERSION=\"21.0.5\"\n");
        var java = Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(
            java,
            "#!/bin/sh\necho 'java.version = 21.0.5' >&2\necho 'java.runtime.name = ScriptedRuntime' >&2\n");
        File.SetUnixFileMode(
            java,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var providers = new List<IJvmProvider> { new StubJvmProvider("stub", home) };

        var probed = await JdkFinder.LocateAsync(
            new JdkFindOptions { Providers = providers, ProbeRuntimeProperties = true }, TestContext.Current.CancellationToken);
        var unprobed = await JdkFinder.LocateAsync(
            new JdkFindOptions { Providers = providers, ProbeRuntimeProperties = false }, TestContext.Current.CancellationToken);

        Assert.Equal("ScriptedRuntime", probed[0].RuntimeName);
        Assert.Null(unprobed[0].RuntimeName);
    }

    [Fact]
    public async Task ProbeAsync_SpawnFailureStillDegradesToNull()
    {
        // The fixture's bin/java is a text file: spawning it fails and the probe
        // degrades silently, with and without a live cancellation token.
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        Assert.Null(await JvmRuntimeProbe.ProbeAsync(jdk, CancellationToken.None));
    }

    [Fact]
    public async Task ReleaseFileParse_PreCancelledToken_Throws()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Task.Run(() => ReleaseFile.Parse(Path.Combine(jdk, "release"), Cancelled)));
    }

    public void Dispose() => temp.Dispose();
}
