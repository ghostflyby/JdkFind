using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Channels;

namespace JdkFind;

/// <summary>
///     Options for <see cref="JvmMonitor" />.
/// </summary>
public sealed record JvmMonitorOptions
{
    /// <summary>
    ///     How often the monitor rescans the configured sources. Zero or negative
    ///     disables timed rescans; <see cref="JvmMonitor.RequestRefresh" /> still
    ///     forces one.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Watch the top level of each located home directory for file-system
    ///     changes. Watchers are hints only: polling and explicit refreshes remain
    ///     the correctness baseline, because watch semantics differ across
    ///     platforms and a watched path does not follow symbolic-link retargets.
    /// </summary>
    public bool WatchHomes { get; init; } = true;
}

/// <summary>
///     One monitor scan: the complete current state plus the changes relative to
///     the previous update the monitor emitted.
/// </summary>
public sealed record JvmUpdate
{
    /// <summary>The complete set of JVMs visible at scan time, in discovery order.</summary>
    public required IReadOnlyList<Jvm> Snapshot { get; init; }

    /// <summary>Homes that appeared since the previous update.</summary>
    public required IReadOnlyList<Jvm> Added { get; init; }

    /// <summary>Homes that disappeared since the previous update.</summary>
    public required IReadOnlyList<Jvm> Removed { get; init; }

    /// <summary>Homes whose metadata changed since the previous update.</summary>
    public required IReadOnlyList<JvmChange> Changed { get; init; }

    /// <summary>Diffs two snapshots by canonical home path; the first update
    /// against an empty snapshot reports every home as added. With
    /// <see cref="JdkFinder.DeduplicateHomes" /> disabled, entries that resolve to
    /// the same physical directory collapse in the diff.</summary>
    internal static JvmUpdate Diff(IReadOnlyList<Jvm> previous, IReadOnlyList<Jvm> current)
    {
        var previousByKey = new Dictionary<string, Jvm>(PathComparison.Comparer);
        foreach (var jvm in previous)
            previousByKey[Identity(jvm)] = jvm;

        var currentKeys = new HashSet<string>(PathComparison.Comparer);
        var added = new List<Jvm>();
        var changed = new List<JvmChange>();

        foreach (var jvm in current)
        {
            var key = Identity(jvm);
            currentKeys.Add(key);
            if (!previousByKey.TryGetValue(key, out var before))
                added.Add(jvm);
            else if (!before.Equals(jvm))
                changed.Add(new JvmChange { Previous = before, Current = jvm });
        }

        var removed = new List<Jvm>();
        foreach (var jvm in previous)
        {
            if (!currentKeys.Contains(Identity(jvm)))
                removed.Add(jvm);
        }

        return new JvmUpdate
        {
            Snapshot = current,
            Added = added,
            Removed = removed,
            Changed = changed,
        };
    }

    private static string Identity(Jvm jvm) => JdkFinder.GetDeduplicationKey(jvm.Home.FullName);
}

/// <summary>A JVM whose metadata changed between two monitor scans.</summary>
public sealed record JvmChange
{
    /// <summary>The JVM as the previous update reported it.</summary>
    public required Jvm Previous { get; init; }

    /// <summary>The JVM as the current update reports it.</summary>
    public required Jvm Current { get; init; }
}

/// <summary>
///     A long-running view of the machine's JVMs. The monitor scans once when
///     <see cref="WatchAsync" /> starts and then rescans on its poll interval,
///     on <see cref="RequestRefresh" />, when the finder is replaced through
///     <see cref="UpdateFinder" />, or when one of the located home directories
///     reports a top-level file-system change. Every update carries the complete
///     snapshot; the deltas are relative to the update the caller last received.
/// </summary>
public sealed class JvmMonitor
{
    private readonly JvmMonitorOptions _options;
    private readonly Channel<byte> _refreshRequests;
    private JdkFinder _finder;
    private int _active;

    /// <summary>Creates a monitor over <paramref name="finder" />.</summary>
    public JvmMonitor(JdkFinder finder, JvmMonitorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(finder);
        _finder = finder;
        _options = options ?? new JvmMonitorOptions();
        _refreshRequests = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    ///     Replaces the finder future scans use — the input-side hook for callers
    ///     that own the environment and therefore know when it changed. The next
    ///     scan diffs against the previous snapshot, so sources that disappear and
    ///     appear surface through <see cref="JvmUpdate.Removed" /> and
    ///     <see cref="JvmUpdate.Added" />, and a home reported by a different
    ///     provider set surfaces through <see cref="JvmUpdate.Changed" />.
    /// </summary>
    public void UpdateFinder(JdkFinder finder)
    {
        ArgumentNullException.ThrowIfNull(finder);
        Volatile.Write(ref _finder, finder);
        RequestRefresh();
    }

    /// <summary>Requests a scan; repeated requests collapse into one pending scan.</summary>
    public void RequestRefresh() => _refreshRequests.Writer.TryWrite(0);

    /// <summary>
    ///     Streams updates until <paramref name="cancellationToken" /> is
    ///     cancelled. The first item is the initial scan, so its
    ///     <see cref="JvmUpdate.Added" /> list contains every visible home. Scans
    ///     are demand-driven: the next scan starts when the caller requests the
    ///     next update, so a slow consumer observes fewer, later snapshots rather
    ///     than a growing queue. A monitor supports one active enumeration at a
    ///     time; dispose the enumerator or cancel the token to release it.
    /// </summary>
    public async IAsyncEnumerable<JvmUpdate> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "A JvmMonitor supports one active WatchAsync enumeration at a time.");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watchers = new List<FileSystemWatcher>();
        PeriodicTimer? timer = null;
        Task? timerPump = null;
        try
        {
            var previous = (IReadOnlyList<Jvm>)Array.Empty<Jvm>();
            var current = await ScanAsync(stop.Token).ConfigureAwait(false);
            var update = JvmUpdate.Diff(previous, current);
            previous = current;
            yield return update;

            if (_options.WatchHomes)
                ReconcileWatchers(current, watchers);

            if (_options.PollInterval > TimeSpan.Zero)
            {
                timer = new PeriodicTimer(_options.PollInterval);
                timerPump = PumpAsync(timer, stop.Token);
            }

            while (true)
            {
                await _refreshRequests.Reader.ReadAsync(stop.Token).ConfigureAwait(false);

                // Scans are serialized; everything requested while one was running
                // is satisfied by the next scan.
                while (_refreshRequests.Reader.TryRead(out _))
                {
                }

                current = await ScanAsync(stop.Token).ConfigureAwait(false);
                update = JvmUpdate.Diff(previous, current);
                previous = current;
                yield return update;

                if (_options.WatchHomes)
                    ReconcileWatchers(current, watchers);
            }
        }
        finally
        {
            stop.Cancel();
            foreach (var watcher in watchers)
                watcher.Dispose();

            timer?.Dispose();
            if (timerPump is not null)
            {
                try
                {
                    await timerPump.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown.
                }
            }

            Volatile.Write(ref _active, 0);
        }
    }

    private Task<IReadOnlyList<Jvm>> ScanAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _finder).LocateAsync(cancellationToken);

    private async Task PumpAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            RequestRefresh();
    }

    /// <summary>
    ///     Replaces the home watchers with one non-recursive watcher per located
    ///     home. A home that cannot be watched still participates through polling.
    /// </summary>
    private void ReconcileWatchers(IReadOnlyList<Jvm> snapshot, List<FileSystemWatcher> watchers)
    {
        foreach (var watcher in watchers)
            watcher.Dispose();
        watchers.Clear();

        foreach (var jvm in snapshot)
        {
            try
            {
                var watcher = new FileSystemWatcher(jvm.Home.FullName)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                watcher.Changed += OnFileSystemEvent;
                watcher.Created += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
            {
                // Watching is an optimization; scanning still reports this home.
            }
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => RequestRefresh();

    private void OnRenamed(object sender, RenamedEventArgs e) => RequestRefresh();

    private void OnWatcherError(object sender, ErrorEventArgs e) => RequestRefresh();
}
