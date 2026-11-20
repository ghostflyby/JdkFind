using System.Threading.Channels;

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

    private sealed class CountingProvider(IReadOnlyList<string> homes) : IJvmProvider
    {
        private int _calls;

        public string Name => "counting";

        public int Calls => Volatile.Read(ref _calls);

        public IEnumerable<string> GetJavaHomes()
        {
            Interlocked.Increment(ref _calls);
            return homes;
        }
    }

    private sealed class DelayGate
    {
        private readonly Channel<TaskCompletionSource> _delays =
            Channel.CreateUnbounded<TaskCompletionSource>();

        internal Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _delays.Writer.TryWrite(completion);
            return completion.Task.WaitAsync(cancellationToken);
        }

        internal async Task<TaskCompletionSource> NextAsync(CancellationToken cancellationToken) =>
            await _delays.Reader.ReadAsync(cancellationToken);
    }

    private static JdkFinder FinderFor(IJvmProvider provider) =>
        new() { Providers = [provider], ProbeRuntimeProperties = false };

    private static JvmMonitor CreateMonitor(IJvmProvider provider) =>
        new(FinderFor(provider), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = false,
            SettleInterval = TimeSpan.Zero,
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
            // Poll ticks must not restart the settle window; otherwise a poll
            // shorter than the window would starve every scan.
            SettleInterval = TimeSpan.FromMilliseconds(100),
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
            SettleInterval = TimeSpan.Zero,
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
    public void Constructor_RejectsIntervalsBeyondTheSystemTimer()
    {
        var finder = FinderFor(new StubProvider("stub", []));

        Assert.Throws<ArgumentOutOfRangeException>(() => new JvmMonitor(
            finder, new JvmMonitorOptions { PollInterval = TimeSpan.MaxValue }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JvmMonitor(
            finder, new JvmMonitorOptions { SettleInterval = TimeSpan.MaxValue }));
    }

    [Fact]
    public async Task WatchAsync_RestartsTheSettleWindowOnActivity()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var provider = new CountingProvider([home]);
        var gate = new DelayGate();
        var monitor = new JvmMonitor(FinderFor(provider), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = false,
            SettleInterval = TimeSpan.FromMilliseconds(1),
        }, gate.Delay);
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(enumerator, token);
        Assert.Equal(1, provider.Calls);

        // Resume the loop first; it is now waiting for refresh requests.
        var next = enumerator.MoveNextAsync().AsTask();
        monitor.RequestRefresh();
        var first = await gate.NextAsync(token).WaitAsync(TimeSpan.FromSeconds(10), token);

        // Activity while the window is open must restart it, not scan immediately.
        monitor.RequestRefresh();
        first.SetResult();

        var second = await gate.NextAsync(token).WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.False(next.IsCompleted);
        Assert.Equal(1, provider.Calls);

        second.SetResult();
        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(10), token));
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task WatchAsync_RecursiveHomeWatcherDetectsNestedChanges()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = new JvmMonitor(FinderFor(new StubProvider("stub", [home])), new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = true,
            SettleInterval = TimeSpan.FromMilliseconds(100),
        });
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await NextAsync(enumerator, token);

        // A non-recursive watcher sees only the top level; this nested write must
        // still schedule a settled scan.
        File.WriteAllText(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), "changed");

        var update = await NextAsync(enumerator, token);
        Assert.Single(update.Snapshot);
        Assert.Empty(update.Added);
        Assert.Empty(update.Removed);
        Assert.Empty(update.Changed);
    }

    [Fact]
    public async Task WatchAsync_CollapsesDuplicatePhysicalHomes()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var finder = new JdkFinder
        {
            Providers = [new StubProvider("first", [home]), new StubProvider("second", [home])],
            DeduplicateHomes = false,
            ProbeRuntimeProperties = false,
        };
        var monitor = new JvmMonitor(finder, new JvmMonitorOptions
        {
            PollInterval = TimeSpan.Zero,
            WatchHomes = false,
            SettleInterval = TimeSpan.Zero,
        });
        var token = TestContext.Current.CancellationToken;

        await using var enumerator = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var initial = await NextAsync(enumerator, token);

        var jvm = Assert.Single(initial.Snapshot);
        Assert.Equal(["first"], jvm.Providers);
        Assert.Single(initial.Added);
        Assert.Empty(initial.Removed);
    }

    [Fact]
    public async Task WatchAsync_CanRunAgainAfterTheFirstEnumerationEnds()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor(new StubProvider("stub", [home]));
        var token = TestContext.Current.CancellationToken;

        await using (var first = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken))
        {
            _ = await NextAsync(first, token);
        }

        await using var second = monitor.WatchAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var update = await NextAsync(second, token);
        Assert.Single(update.Snapshot);
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
