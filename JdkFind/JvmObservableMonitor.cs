using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JdkFind;

/// <summary>
///     A data-binding adapter over <see cref="JvmMonitor" />. Maintains the
///     current installations in a <see cref="ReadOnlyObservableCollection{T}" />
///     and raises <see cref="PropertyChanged" /> for the state a view binds to.
///     Notifications are marshalled to the synchronization context captured by
///     <see cref="Start" /> — normally the UI thread's — so XAML, WinForms and
///     MAUI bindings observe them on the thread they expect.
///     A changed installation is replaced at its position
///     (<see cref="System.Collections.Specialized.NotifyCollectionChangedAction.Replace" />),
///     so item templates refresh without making <see cref="Jvm" /> mutable.
/// </summary>
public sealed class JvmObservableMonitor : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly JvmMonitor _monitor;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly ObservableCollection<Jvm> _jvms = [];
    private readonly Dictionary<Jvm, string> _identities = new(ReferenceEqualityComparer.Instance);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _completion;
    private int _disposed;

    private JvmObservableMonitor(JvmMonitor monitor, SynchronizationContext? synchronizationContext)
    {
        _monitor = monitor;
        _synchronizationContext = synchronizationContext;
        Jvms = new ReadOnlyObservableCollection<Jvm>(_jvms);
        _completion = Task.Run(RunAsync);
    }

    /// <summary>
    ///     Starts observing <paramref name="monitor" />. When
    ///     <paramref name="synchronizationContext" /> is null the current
    ///     context is captured; call this from the UI thread so notifications
    ///     arrive there, or pass that thread's context explicitly when starting
    ///     from a background thread. With no context at all, updates are
    ///     applied on the monitoring loop's thread.
    /// </summary>
    public static JvmObservableMonitor Start(
        JvmMonitor monitor, SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return new JvmObservableMonitor(monitor, synchronizationContext ?? SynchronizationContext.Current);
    }

    /// <summary>The current installations, kept in scan order.</summary>
    public ReadOnlyObservableCollection<Jvm> Jvms { get; }

    /// <summary>The number of entries in <see cref="Jvms" />, updated after each applied scan.</summary>
    public int Count
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True until the monitoring loop stops, whether by disposal or error.</summary>
    public bool IsRunning
    {
        get;
        private set => SetProperty(ref field, value);
    } = true;

    /// <summary>The last applied scan, or null before the initial scan completes.</summary>
    public JvmUpdate? LastUpdate
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>When the last update was applied, or null before the initial scan.</summary>
    public DateTimeOffset? LastUpdated
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The error that stopped the monitoring loop, or null while it is healthy.</summary>
    public string? LastError
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Completes when the monitoring loop stops, whether by disposal or error.</summary>
    public Task Completion => _completion;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _stop.CancelAsync();
            await _completion.ConfigureAwait(false);
            _stop.Dispose();
            return;
        }

        await _completion.ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var update in _monitor.WatchAsync(_stop.Token).ConfigureAwait(false))
            {
                // Canonical-path work stays on the monitoring thread; the UI thread
                // only applies collection mutations and property notifications.
                var identities = BuildIdentities(update);
                await MarshalAsync(() => Apply(update, identities), _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Disposal.
        }
        catch (Exception exception)
        {
            await ApplyBestEffortAsync(() => LastError = exception.Message).ConfigureAwait(false);
        }
        finally
        {
            await ApplyBestEffortAsync(() => IsRunning = false).ConfigureAwait(false);
        }
    }

    private static Dictionary<Jvm, string> BuildIdentities(JvmUpdate update)
    {
        var identities = new Dictionary<Jvm, string>(ReferenceEqualityComparer.Instance);
        foreach (var jvm in update.Snapshot)
            identities[jvm] = JvmUpdate.Identity(jvm);
        foreach (var jvm in update.Removed)
            identities.TryAdd(jvm, JvmUpdate.Identity(jvm));
        foreach (var change in update.Changed)
        {
            identities.TryAdd(change.Previous, JvmUpdate.Identity(change.Previous));
            identities.TryAdd(change.Current, JvmUpdate.Identity(change.Current));
        }

        return identities;
    }

    /// <summary>Applies one update on the captured context: remove, replace, insert.</summary>
    private void Apply(JvmUpdate update, IReadOnlyDictionary<Jvm, string> identities)
    {
        string KeyOf(Jvm jvm) =>
            identities.TryGetValue(jvm, out var key) ? key
            : _identities.TryGetValue(jvm, out key) ? key
            : JvmUpdate.Identity(jvm);

        foreach (var removed in update.Removed)
            Remove(removed, KeyOf);

        foreach (var change in update.Changed)
            Replace(change, KeyOf);

        foreach (var added in update.Added)
        {
            var key = KeyOf(added);
            if (IndexOf(key) >= 0)
                continue;

            _jvms.Insert(InsertionIndex(added, update.Snapshot, KeyOf), added);
            _identities[added] = key;
        }

        LastUpdate = update;
        LastUpdated = DateTimeOffset.UtcNow;
        Count = _jvms.Count;
    }

    /// <summary>Finds the position an added home occupies in the new snapshot so
    /// insertions keep scan order relative to the entries already present.</summary>
    private int InsertionIndex(Jvm added, IReadOnlyList<Jvm> snapshot, Func<Jvm, string> keyOf)
    {
        var addedIndex = snapshot.Count;
        for (var index = 0; index < snapshot.Count; index++)
        {
            if (ReferenceEquals(snapshot[index], added))
            {
                addedIndex = index;
                break;
            }
        }

        for (var index = addedIndex + 1; index < snapshot.Count; index++)
        {
            var existing = IndexOf(keyOf(snapshot[index]));
            if (existing >= 0)
                return existing;
        }

        return _jvms.Count;
    }

    private void Remove(Jvm removed, Func<Jvm, string> keyOf)
    {
        var index = ReferenceIndexOf(removed);
        if (index < 0)
            index = IndexOf(keyOf(removed));

        if (index < 0)
            return;

        _identities.Remove(_jvms[index]);
        _jvms.RemoveAt(index);
    }

    /// <summary>Replaces by the previous instance, falling back to canonical
    /// identity; replacement keeps <see cref="ReadOnlyObservableCollection{T}" />
    /// subscribers on the Replace path instead of a remove/add pair.</summary>
    private void Replace(JvmChange change, Func<Jvm, string> keyOf)
    {
        var index = ReferenceIndexOf(change.Previous);
        if (index < 0)
            index = IndexOf(keyOf(change.Current));

        if (index < 0)
            return;

        _identities.Remove(_jvms[index]);
        _jvms[index] = change.Current;
        _identities[change.Current] = keyOf(change.Current);
    }

    private int ReferenceIndexOf(Jvm jvm)
    {
        for (var index = 0; index < _jvms.Count; index++)
        {
            if (ReferenceEquals(_jvms[index], jvm))
                return index;
        }

        return -1;
    }

    private int IndexOf(string identity)
    {
        for (var index = 0; index < _jvms.Count; index++)
        {
            var key = _identities.TryGetValue(_jvms[index], out var cached)
                ? cached
                : JvmUpdate.Identity(_jvms[index]);
            if (PathComparison.Comparer.Equals(key, identity))
                return index;
        }

        return -1;
    }

    /// <summary>Terminal notifications must not keep <see cref="Completion" /> (and
    /// therefore <see cref="DisposeAsync" />) hostage when the synchronization
    /// context stopped pumping; fall back to an inline update after a bounded wait.</summary>
    private async Task ApplyBestEffortAsync(Action action)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await MarshalAsync(action, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            action();
        }
    }

    /// <summary>Runs <paramref name="action" /> on the captured context and
    /// completes after it has been applied, so updates stay ordered and a slow
    /// UI simply pauses the monitoring loop.</summary>
    private Task MarshalAsync(Action action, CancellationToken cancellationToken)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _synchronizationContext.Post(_ =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }, null);

        return completion.Task.WaitAsync(cancellationToken);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
