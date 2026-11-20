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
    ///     Watch each located home directory recursively. Every write under a home
    ///     can affect whether the installation is runnable — an upgrade may replace
    ///     <c>lib</c>, <c>bin</c> or the release file in any order — so nested
    ///     changes matter. Polling remains the correctness baseline: watch
    ///     descriptors can be exhausted, event queues can overflow, and a watched
    ///     path does not follow symbolic-link retargets.
    /// </summary>
    public bool WatchHomes { get; init; } = true;

    /// <summary>
    ///     How long file-system events must stay quiet before a scan starts. A
    ///     package upgrade writes many files; probing the installation mid-write can
    ///     run a half-replaced <c>java</c>. The monitor restarts this window on every
    ///     event, so a burst produces one scan after it settles. Zero disables the
    ///     quiet period.
    /// </summary>
    public TimeSpan SettleInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>
///     One monitor scan: the complete current state plus the changes relative to
///     the previous update the monitor emitted.
/// </summary>
public sealed record JvmUpdate
{
    /// <summary>The complete set of JVMs visible at scan time, in discovery order.
    /// Entries that resolve to the same physical directory collapse to the first
    /// reported one, even when <see cref="JdkFinder.DeduplicateHomes" /> is disabled.</summary>
    public required IReadOnlyList<Jvm> Snapshot { get; init; }

    /// <summary>Homes that appeared since the previous update.</summary>
    public required IReadOnlyList<Jvm> Added { get; init; }

    /// <summary>Homes that disappeared since the previous update.</summary>
    public required IReadOnlyList<Jvm> Removed { get; init; }

    /// <summary>Homes whose metadata changed since the previous update.</summary>
    public required IReadOnlyList<JvmChange> Changed { get; init; }

    /// <summary>Diffs two snapshots by canonical home path; the first update
    /// against an empty snapshot reports every home as added. The monitor
    /// identifies an installation by its physical directory, so duplicate entries
    /// collapse to the first reported one even when
    /// <see cref="JdkFinder.DeduplicateHomes" /> is disabled.</summary>
    internal static JvmUpdate Diff(IReadOnlyList<Jvm> previous, IReadOnlyList<Jvm> current)
    {
        var previousByKey = new Dictionary<string, Jvm>(PathComparison.Comparer);
        foreach (var jvm in previous)
            previousByKey[Identity(jvm)] = jvm;

        var currentKeys = new HashSet<string>(PathComparison.Comparer);
        var unique = new List<Jvm>(current.Count);
        var added = new List<Jvm>();
        var changed = new List<JvmChange>();

        foreach (var jvm in current)
        {
            var key = Identity(jvm);
            if (!currentKeys.Add(key))
                continue;

            unique.Add(jvm);
            if (!previousByKey.TryGetValue(key, out var before))
                added.Add(jvm);
            else if (!before.Equals(jvm))
                changed.Add(new JvmChange { Previous = before, Current = jvm });
        }

        var removedKeys = new HashSet<string>(PathComparison.Comparer);
        var removed = new List<Jvm>();
        foreach (var jvm in previous)
        {
            var key = Identity(jvm);
            if (!currentKeys.Contains(key) && removedKeys.Add(key))
                removed.Add(jvm);
        }

        return new JvmUpdate
        {
            Snapshot = unique.AsReadOnly(),
            Added = added,
            Removed = removed,
            Changed = changed,
        };
    }

    internal static string Identity(Jvm jvm) => JdkFinder.GetDeduplicationKey(jvm.Home.FullName);
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
///     <see cref="UpdateFinder" />, or when a located home reports a file-system
///     change. Homes are watched recursively, and a burst of events is debounced
///     until it stays quiet for <see cref="JvmMonitorOptions.SettleInterval" />
///     before the scan starts. Every update carries the complete snapshot; the
///     deltas are relative to the update the caller last received.
/// </summary>
public sealed class JvmMonitor
{
    private static readonly TimeSpan MaxInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly JvmMonitorOptions _options;
    private readonly Channel<byte> _refreshRequests;
    private readonly Func<TimeSpan, CancellationToken, Task> _quietDelay;
    private JdkFinder _finder;
    private int _active;
    private long _activityVersion;

    /// <summary>Creates a monitor over <paramref name="finder" />.</summary>
    public JvmMonitor(JdkFinder finder, JvmMonitorOptions? options = null)
        : this(finder, options, static (interval, token) => Task.Delay(interval, token))
    {
    }

    internal JvmMonitor(
        JdkFinder finder,
        JvmMonitorOptions? options,
        Func<TimeSpan, CancellationToken, Task> quietDelay)
    {
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(quietDelay);
        _finder = finder;
        _options = options ?? new JvmMonitorOptions();
        _quietDelay = quietDelay;

        if (_options.PollInterval > MaxInterval)
            throw new ArgumentOutOfRangeException(nameof(options), _options.PollInterval,
                "The poll interval is too long for the system timer.");
        if (_options.SettleInterval > MaxInterval)
            throw new ArgumentOutOfRangeException(nameof(options), _options.SettleInterval,
                "The settle interval is too long for the system timer.");

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

    /// <summary>Requests a scan; repeated requests collapse into one pending scan.
    /// A request extends the settle window because the caller is reacting to a
    /// change; the poll timer deliberately does not.</summary>
    public void RequestRefresh()
    {
        Interlocked.Increment(ref _activityVersion);
        _refreshRequests.Writer.TryWrite(0);
    }

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

        var watchers = new Dictionary<string, WatchedHome>(PathComparison.Comparer);
        CancellationTokenSource? stop = null;
        PeriodicTimer? timer = null;
        Task? timerPump = null;
        try
        {
            stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = stop.Token;

            var previous = (IReadOnlyList<Jvm>)Array.Empty<Jvm>();
            var current = await ScanAsync(token).ConfigureAwait(false);
            var update = JvmUpdate.Diff(previous, current);
            previous = update.Snapshot;

            // Activate watchers before the caller sees the snapshot: otherwise a
            // change between the update and the next MoveNext would be missed.
            if (_options.WatchHomes)
                ReconcileWatchers(update.Snapshot, watchers);
            yield return update;

            if (_options.PollInterval > TimeSpan.Zero)
            {
                timer = new PeriodicTimer(_options.PollInterval);
                timerPump = PumpAsync(timer, token);
            }

            while (true)
            {
                await _refreshRequests.Reader.ReadAsync(token).ConfigureAwait(false);

                // An upgrade writes in bursts; wait until the burst has been quiet
                // for the settle window so the scan cannot probe a half-replaced home.
                await WaitForQuietAsync(token).ConfigureAwait(false);

                // Scans are serialized; everything requested while one was running
                // is satisfied by the next scan.
                while (_refreshRequests.Reader.TryRead(out _))
                {
                }

                current = await ScanAsync(token).ConfigureAwait(false);
                update = JvmUpdate.Diff(previous, current);
                previous = update.Snapshot;

                if (_options.WatchHomes)
                    ReconcileWatchers(update.Snapshot, watchers);
                yield return update;
            }
        }
        finally
        {
            try
            {
                stop?.Cancel();
                foreach (var watchedHome in watchers.Values)
                    watchedHome.Watcher.Dispose();

                if (timerPump is not null)
                {
                    try
                    {
                        // Cancel first, then let the pump observe it; disposing the
                        // timer before the pump finishes can make it throw
                        // ObjectDisposedException out of this finally.
                        await timerPump.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown.
                    }
                    catch (ObjectDisposedException)
                    {
                        // A pump that raced with timer disposal.
                    }
                }
            }
            finally
            {
                timer?.Dispose();
                stop?.Dispose();
                Volatile.Write(ref _active, 0);
            }
        }
    }

    private Task<IReadOnlyList<Jvm>> ScanAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _finder).LocateAsync(cancellationToken);

    private async Task PumpAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            RequestPoll();
    }

    /// <summary>Wakes the scan loop without extending the quiet window; the poll
    /// timer must not feed its own debounce.</summary>
    private void RequestPoll() => _refreshRequests.Writer.TryWrite(0);

    /// <summary>
    ///     Incrementally reconciles one recursive watcher per canonical home. Homes
    ///     that are still present keep their watcher; only additions, removals and a
    ///     changed reported path (for example a retargeted symlink) create or drop
    ///     one. A home that cannot be watched still participates through polling.
    /// </summary>
    private void ReconcileWatchers(
        IReadOnlyList<Jvm> snapshot, Dictionary<string, WatchedHome> watchers)
    {
        var seen = new HashSet<string>(PathComparison.Comparer);
        foreach (var jvm in snapshot)
        {
            var identity = JvmUpdate.Identity(jvm);
            if (!seen.Add(identity))
                continue;

            var homePath = jvm.Home.FullName;
            if (watchers.TryGetValue(identity, out var existing))
            {
                if (PathComparison.Comparer.Equals(existing.Path, homePath) && !existing.Failed)
                    continue;

                existing.Watcher.Dispose();
                watchers.Remove(identity);
            }

            if (TryCreateWatcher(homePath) is { } watchedHome)
                watchers[identity] = watchedHome;
        }

        List<string>? removed = null;
        foreach (var identity in watchers.Keys)
        {
            if (!seen.Contains(identity))
                (removed ??= []).Add(identity);
        }

        if (removed is null)
            return;

        foreach (var identity in removed)
        {
            watchers[identity].Watcher.Dispose();
            watchers.Remove(identity);
        }
    }

    private WatchedHome? TryCreateWatcher(string homePath)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(homePath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            var watchedHome = new WatchedHome(homePath, watcher);
            watcher.Changed += OnFileSystemEvent;
            watcher.Created += OnFileSystemEvent;
            watcher.Deleted += OnFileSystemEvent;
            watcher.Renamed += OnRenamed;
            watcher.Error += (_, _) =>
            {
                // An overflow or a dead handle: let the next scan recreate the
                // watcher instead of silently degrading to polling.
                watchedHome.Failed = true;
                RequestRefresh();
            };
            watcher.EnableRaisingEvents = true;
            return watchedHome;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
        {
            watcher?.Dispose();
            return null;
        }
    }

    /// <summary>Waits until no file-system activity arrives for the settle interval.
    /// The activity version is bumped by watcher events and explicit refreshes, not
    /// by the poll timer, so polling cannot keep restarting its own quiet window.
    /// The scan therefore starts only after an upgrade's write burst has stopped.</summary>
    private async Task WaitForQuietAsync(CancellationToken cancellationToken)
    {
        var settleInterval = _options.SettleInterval;
        if (settleInterval <= TimeSpan.Zero)
            return;

        var observed = Volatile.Read(ref _activityVersion);
        while (true)
        {
            await _quietDelay(settleInterval, cancellationToken).ConfigureAwait(false);

            var current = Volatile.Read(ref _activityVersion);
            if (current == observed)
                return;

            observed = current;
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => RequestRefresh();

    private void OnRenamed(object sender, RenamedEventArgs e) => RequestRefresh();

    private sealed class WatchedHome(string path, FileSystemWatcher watcher)
    {
        internal string Path { get; } = path;

        internal FileSystemWatcher Watcher { get; } = watcher;

        internal volatile bool Failed;
    }
}
