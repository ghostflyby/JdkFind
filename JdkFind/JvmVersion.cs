using System.Numerics;

namespace JdkFind;

/// <summary>
///     A parsed <c>JAVA_VERSION</c> value that compares the way JDK releases do:
///     numeric components first (up to four; missing components are zero), then a
///     release beats an early-access build of the same number. Note this is not
///     SemVer — real JDK versions like <c>21.0.12.1</c> and <c>1.8.0_402</c> are
///     outside the SemVer grammar.
/// </summary>
public readonly record struct JvmVersion : IComparable<JvmVersion>,
    IComparisonOperators<JvmVersion, JvmVersion, bool>
{
    /// <summary>The normalized numeric version (up to four components; missing components are zero).</summary>
    public Version Core { get; }

    /// <summary>True when the value carried a pre-release tag (e.g. <c>25-ea</c>).</summary>
    public bool IsPreRelease { get; }

    /// <summary>The raw <c>JAVA_VERSION</c> string as found in the release file.</summary>
    public string Original { get; }

    internal JvmVersion(Version core, bool isPreRelease, string original)
    {
        Core = core;
        IsPreRelease = isPreRelease;
        Original = original;
    }

    /// <summary>Parses a raw <c>JAVA_VERSION</c> value (<c>21.0.5</c>, <c>1.8.0_402</c>, <c>25-ea</c>, <c>27</c>, ...).</summary>
    public static bool TryParse(string? javaVersion, out JvmVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(javaVersion))
            return false;

        // The pre-release tag starts at the first '-'; a '+' build suffix ends the string.
        var head = CutAt(javaVersion, '-');
        var isPreRelease = !ReferenceEquals(head, javaVersion);
        head = CutAt(head, '+').Replace('_', '.');

        // System.Version requires at least two segments; pad "27" to "27.0".
        if (!head.Contains('.'))
            head += ".0";

        if (!Version.TryParse(head, out var core))
            return false;

        version = new JvmVersion(core, isPreRelease, javaVersion);
        return true;
    }

    /// <summary>
    ///     Parses a raw <c>JAVA_VERSION</c> value. Unparseable input degrades to an
    ///     <see cref="Unknown" /> placeholder, so a JVM is never lost over its version
    ///     string.
    /// </summary>
    public static JvmVersion Parse(string? javaVersion) =>
        TryParse(javaVersion, out var version) ? version : Unknown(javaVersion ?? string.Empty);

    /// <summary>A placeholder for unparseable values; it sorts before every known version.</summary>
    public static JvmVersion Unknown(string original = "") => new(new Version(0, 0), false, original);

    public int CompareTo(JvmVersion other)
    {
        var core = Core.CompareTo(other.Core);

        // Same number: the release outranks its pre-release builds.
        return core != 0 ? core : other.IsPreRelease.CompareTo(IsPreRelease);
    }

    public static bool operator <(JvmVersion left, JvmVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(JvmVersion left, JvmVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(JvmVersion left, JvmVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(JvmVersion left, JvmVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => Original;

    private static string CutAt(string value, char separator)
    {
        var cut = value.IndexOf(separator);
        return cut < 0 ? value : value[..cut];
    }
}