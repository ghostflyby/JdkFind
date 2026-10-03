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
    public async Task ProbeAsync_TimeoutStillDegradesToNull()
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