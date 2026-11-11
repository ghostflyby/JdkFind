namespace JdkFind.Tests;

public class JvmMonitorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private sealed class StubProvider(string name, IReadOnlyList<string> homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    private static JdkFinder FinderFor(IJvmProvider provider) =>
        new() { Providers = [provider], ProbeRuntimeProperties = false };

    private static JvmMonitor CreateMonitor(IJvmProvider provider) =>
        new(FinderFor(provider), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = false,
        });

    private static async Task<JvmUpdate> NextAsync(
        IAsyncEnumerator<JvmUpdate> enumerator, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(timeout.Token));
        return enumerator.Current;
    }

    [Fact]
    public async Task WatchAsync_EmitsInitialSnapshotAndDeltas()
    {
        var first = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var second = TestJdk.Create(_temp.FullPath, "17.0.10", "jdk-17");
        var homes = new List<string> { first, second };
        var monitor = CreateMonitor(new StubProvider("stub", homes));
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);

        var initial = await NextAsync(enumerator, token);
        Assert.Equal(2, initial.Snapshot.Count);
        Assert.Equal(2, initial.Added.Count);
        Assert.Empty(initial.Removed);
        Assert.Empty(initial.Changed);

        var third = TestJdk.Create(_temp.FullPath, "23.0.1", "jdk-23");
        homes.Add(third);
        monitor.RequestRefresh();

        var addition = await NextAsync(enumerator, token);
        var added = Assert.Single(addition.Added);
        Assert.Equal(third, added.Home.FullName);
        Assert.Equal(3, addition.Snapshot.Count);
        Assert.Empty(addition.Removed);

        // Rewriting the release file keeps the home but changes its metadata.
        TestJdk.Create(_temp.FullPath, "25.0.1", "jdk-21");
        monitor.RequestRefresh();

        var mutation = await NextAsync(enumerator, token);
        var change = Assert.Single(mutation.Changed);
        Assert.Equal(first, change.Previous.Home.FullName);
        Assert.Equal(first, change.Current.Home.FullName);
        Assert.Equal("21.0.5", change.Previous.Version.Original);
        Assert.Equal("25.0.1", change.Current.Version.Original);
        Assert.Empty(mutation.Added);
        Assert.Empty(mutation.Removed);

        homes.Remove(second);
        monitor.RequestRefresh();

        var removal = await NextAsync(enumerator, token);
        var removed = Assert.Single(removal.Removed);
        Assert.Equal(second, removed.Home.FullName);
        Assert.Empty(removal.Added);
        Assert.Empty(removal.Changed);
        Assert.Equal(2, removal.Snapshot.Count);
    }

    [Fact]
    public async Task UpdateFinder_DiffsAgainstThePreviousSnapshot()
    {
        var old = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-old");
        var replacement = TestJdk.Create(_temp.FullPath, "25.0.1", "jdk-new");
        var monitor = CreateMonitor(new StubProvider("first", [old]));
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(enumerator, token);

        monitor.UpdateFinder(FinderFor(new StubProvider("second", [replacement])));

        var update = await NextAsync(enumerator, token);
        Assert.Equal(replacement, Assert.Single(update.Added).Home.FullName);
        Assert.Equal(old, Assert.Single(update.Removed).Home.FullName);
        Assert.Empty(update.Changed);
        Assert.Single(update.Snapshot);
    }

    [Fact]
    public async Task WatchAsync_PollsOnTheConfiguredInterval()
    {
        var first = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var homes = new List<string> { first };
        var monitor = new JvmMonitor(FinderFor(new StubProvider("stub", homes)), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(50),
            WatchHomes = false,
        });
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(enumerator, token);

        var second = TestJdk.Create(_temp.FullPath, "25.0.1", "jdk-25");
        homes.Add(second);

        var update = await NextAsync(enumerator, token);
        Assert.Equal(second, Assert.Single(update.Added).Home.FullName);
        Assert.Equal(2, update.Snapshot.Count);
    }

    [Fact]
    public async Task WatchAsync_WithHomeWatchers_StillDeliversExplicitRefreshes()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = new JvmMonitor(FinderFor(new StubProvider("stub", [home])), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = true,
        });
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(enumerator, token);

        TestJdk.Create(_temp.FullPath, "25.0.1", "jdk-21");
        monitor.RequestRefresh();

        var update = await NextAsync(enumerator, token);
        Assert.Equal("25.0.1", Assert.Single(update.Changed).Current.Version.Original);
    }

    [Fact]
    public async Task WatchAsync_CancellationEndsTheStream()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor(new StubProvider("stub", [home]));
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // The linked token is the subject of this test, not the test runner's token.
#pragma warning disable xUnit1051
        await using var enumerator = monitor.WatchAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
#pragma warning restore xUnit1051
        _ = await NextAsync(enumerator, cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
    }

    [Fact]
    public async Task WatchAsync_RejectsASecondActiveEnumeration()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor(new StubProvider("stub", [home]));
        var token = TestContext.Current.CancellationToken;

        await using var first = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(first, token);

        await using var second = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.MoveNextAsync().AsTask());
    }
}
