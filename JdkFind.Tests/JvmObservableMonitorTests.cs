using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Channels;

namespace JdkFind.Tests;

public class JvmObservableMonitorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private sealed class StubProvider(string name, IReadOnlyList<string> homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    private sealed class ThrowingProvider : IJvmProvider
    {
        public string Name => "throwing";

        public IEnumerable<string> GetJavaHomes() => throw new InvalidOperationException("boom");
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        private int _posts;

        internal int PostCount => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            d(state);
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Channel<(SendOrPostCallback Callback, object? State)> _queue =
            Channel.CreateUnbounded<(SendOrPostCallback Callback, object? State)>();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Writer.TryWrite((d, state));

        internal async Task RunNextAsync(CancellationToken cancellationToken)
        {
            var (callback, state) = await _queue.Reader.ReadAsync(cancellationToken);
            callback(state);
        }
    }

    private static JvmMonitor CreateMonitor(IReadOnlyList<string> homes) =>
        new(new JdkFinder { Providers = [new StubProvider("stub", homes)], ProbeRuntimeProperties = false },
            new JvmMonitorOptions { PollInterval = TimeSpan.Zero, WatchHomes = false, SettleInterval = TimeSpan.Zero });

    /// <summary>Waits for a property notification that satisfies
    /// <paramref name="condition" />; the initial check covers an update that
    /// landed before the subscription, so no state is polled.</summary>
    private static async Task WaitForAsync(JvmObservableMonitor state, Func<bool> condition)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (condition())
                signal.TrySetResult();
        }

        state.PropertyChanged += OnPropertyChanged;
        try
        {
            if (condition())
                return;

            await signal.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            state.PropertyChanged -= OnPropertyChanged;
        }
    }

    [Fact]
    public async Task Start_ReflectsAddsChangesAndRemovals()
    {
        var first = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var homes = new List<string> { first };
        var monitor = CreateMonitor(homes);
        await using var state = JvmObservableMonitor.Start(monitor, synchronizationContext: null);

        await WaitForAsync(state, () => state.LastUpdated is not null);
        Assert.True(state.IsRunning);
        Assert.Null(state.LastError);
        Assert.NotNull(state.LastUpdate);
        Assert.NotNull(state.LastUpdated);
        Assert.Equal(first, state.Jvms[0].Home.FullName);

        var actions = new ConcurrentQueue<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)state.Jvms).CollectionChanged += (_, e) => actions.Enqueue(e.Action);

        var second = TestJdk.Create(_temp.FullPath, "25.0.1", "jdk-25");
        homes.Add(second);
        monitor.RequestRefresh();

        await WaitForAsync(state, () => state.Count == 2 && state.LastUpdate?.Added.Count == 1);
        Assert.Single(state.LastUpdate!.Added);
        Assert.Contains(NotifyCollectionChangedAction.Add, actions);

        // Rewriting the release file keeps the home but changes its metadata.
        TestJdk.Create(_temp.FullPath, "26.0.1", "jdk-21");
        monitor.RequestRefresh();

        await WaitForAsync(state, () => state.LastUpdate?.Changed.Count == 1);
        Assert.Equal("26.0.1", state.Jvms[0].Version.Original);
        Assert.Single(state.LastUpdate!.Changed);
        Assert.Contains(NotifyCollectionChangedAction.Replace, actions);

        homes.Remove(second);
        monitor.RequestRefresh();

        await WaitForAsync(state, () => state.Count == 1 && state.LastUpdate?.Removed.Count == 1);
        Assert.Single(state.LastUpdate!.Removed);
        Assert.Contains(NotifyCollectionChangedAction.Remove, actions);
        Assert.Equal(first, state.Jvms[0].Home.FullName);
    }

    [Fact]
    public async Task DisposeAsync_StopsTheLoopAndRaisesPropertyChanged()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor([home]);
        var state = JvmObservableMonitor.Start(monitor, synchronizationContext: null);
        try
        {
            await WaitForAsync(state, () => state.Count == 1);

            var properties = new ConcurrentQueue<string?>();
            state.PropertyChanged += (_, e) => properties.Enqueue(e.PropertyName);

            await state.DisposeAsync();

            Assert.False(state.IsRunning);
            Assert.True(state.Completion.IsCompletedSuccessfully);
            Assert.Contains(nameof(JvmObservableMonitor.IsRunning), properties);

            // Disposal is idempotent.
            await state.DisposeAsync();
            Assert.True(state.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            await state.DisposeAsync();
        }
    }

    [Fact]
    public async Task Start_MarshalsNotificationsThroughTheSuppliedContext()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor([home]);
        var context = new InlineSynchronizationContext();

        await using var state = JvmObservableMonitor.Start(monitor, context);

        await WaitForAsync(state, () => state.Count == 1);
        Assert.True(context.PostCount > 0);
    }

    [Fact]
    public async Task Start_AppliesUpdatesOnlyWhenTheCapturedContextPumps()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var monitor = CreateMonitor([home]);
        var context = new QueuedSynchronizationContext();
        var token = TestContext.Current.CancellationToken;

        var state = JvmObservableMonitor.Start(monitor, context);
        try
        {
            // Start runs the scan off the calling thread; nothing reaches the
            // collection until the queued callbacks are pumped.
            Assert.Equal(0, state.Count);
            await context.RunNextAsync(token);
            Assert.Equal(1, state.Count);

            var dispose = state.DisposeAsync().AsTask();
            await context.RunNextAsync(token);
            await dispose;

            Assert.False(state.IsRunning);
        }
        finally
        {
            var disposing = state.DisposeAsync().AsTask();
            while (!disposing.IsCompleted)
                await context.RunNextAsync(token);
            await disposing;
        }
    }

    [Fact]
    public async Task Start_SurfacesMonitorFailuresThroughLastError()
    {
        var monitor = new JvmMonitor(
            new JdkFinder { Providers = [new ThrowingProvider()], ProbeRuntimeProperties = false },
            new JvmMonitorOptions { PollInterval = TimeSpan.Zero, WatchHomes = false, SettleInterval = TimeSpan.Zero });

        await using var state = JvmObservableMonitor.Start(monitor, synchronizationContext: null);
        await WaitForAsync(state, () => !state.IsRunning);
        await state.Completion;

        Assert.Contains("boom", state.LastError);
        Assert.Empty(state.Jvms);
        Assert.True(state.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public void Start_NullMonitor_Throws() =>
        Assert.Throws<ArgumentNullException>(() => JvmObservableMonitor.Start(null!));
}
