namespace JdkFind;

/// <summary>
///     Orders JVMs from oldest to newest: by feature version first, then by the full
///     version number. Unknown values always sort last.
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

        var languageComparison = (x.LanguageVersion ?? -1).CompareTo(y.LanguageVersion ?? -1);
        if (languageComparison != 0)
            return languageComparison;

        var xVersion = ReleaseFile.TryParseVersion(x.Version);
        var yVersion = ReleaseFile.TryParseVersion(y.Version);
        return xVersion switch
        {
            null when yVersion is null => 0,
            null => -1,
            _ => yVersion is null ? 1 : xVersion.CompareTo(yVersion)
        };
    }
}
