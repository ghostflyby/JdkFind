namespace JdkFind;

/// <summary>
///     Orders JVMs from oldest to newest by their parsed version number
///     (<see cref="Jvm.VersionNumber" />); unparsed values always sort last.
/// </summary>
public sealed class JvmVersionComparer : IComparer<Jvm?>
{
    public static JvmVersionComparer Default { get; } = new();

    public int Compare(Jvm? x, Jvm? y)
    {
        if (x is null)
            return y is null ? 0 : -1;
        if (y is null)
            return 1;

        return (x.VersionNumber, y.VersionNumber) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => x.VersionNumber.Value.CompareTo(y.VersionNumber.Value)
        };
    }
}
