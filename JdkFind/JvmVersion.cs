using System.Numerics;
using System.Text;

namespace JdkFind;

/// <summary>
///     A parsed <c>JAVA_VERSION</c> value that compares the way JDK releases do:
///     numeric components first (up to four; missing components are zero), then a
///     release beats a pre-release build of the same number, then the remaining
///     build number and optional tag compare in natural order, so <c>21.0.5+10</c>
///     outranks <c>21.0.5+9-LTS</c>. Unparseable values sort before every parsed
///     version and compare naturally among themselves. Note this is not SemVer —
///     real JDK versions like <c>21.0.12.1</c> and <c>1.8.0_402</c> are outside the
///     SemVer grammar.
/// </summary>
public readonly record struct JvmVersion : IComparable<JvmVersion>,
    IComparisonOperators<JvmVersion, JvmVersion, bool>
{
    /// <summary>The normalized numeric version: four components, missing ones zero,
    /// so <c>17.0.2</c> and <c>17.0.2.0</c> are the same version. A default-constructed
    /// or unparseable value reports the zero version.</summary>
    public Version Core => _core ?? Zero;

    /// <summary>True when the value carried a pre-release tag before its build number
    /// (<c>25-ea</c>, <c>24.0.1-ea+5</c>). False for release builds, including those
    /// whose tag follows the build number (<c>21.0.5+9-LTS</c>) and the legacy JDK 8
    /// build markers (<c>1.8.0_402-b10</c>).</summary>
    public bool IsPreRelease { get; }

    /// <summary>The raw <c>JAVA_VERSION</c> string as found in the release file.</summary>
    public string Original { get; }

    private readonly Version? _core;

    /// <summary>The canonical comparison tail: the text from the first <c>-</c> or
    /// <c>+</c> onwards, or the canonical raw string for an unparseable value.</summary>
    private readonly string _suffix;

    private readonly bool _unparsed;

    private static readonly Version Zero = new(0, 0, 0, 0);

    private JvmVersion(Version core, bool isPreRelease, string suffix, bool unparsed, string original)
    {
        _core = core;
        IsPreRelease = isPreRelease;
        _suffix = Normalize(suffix);
        _unparsed = unparsed;
        Original = original;
    }

    /// <summary>The comparison tail; empty when the value carries none.</summary>
    private string Suffix => _suffix ?? string.Empty;

    /// <summary>True for a placeholder — or a default-constructed value — which
    /// carries no parsed core.</summary>
    private bool IsUnparsed => _unparsed || _core is null;

    /// <summary>Parses a raw <c>JAVA_VERSION</c> value (<c>21.0.5</c>, <c>1.8.0_402</c>,
    /// <c>25-ea</c>, <c>21.0.5+9-LTS</c>, <c>27</c>, ...).</summary>
    public static bool TryParse(string? javaVersion, out JvmVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(javaVersion))
            return false;

        // JEP 223: $VNUM(-$PRE)?(+$BUILD(-$OPT)?)? — the numeric core ends at the
        // first '-' or '+' (a -1 index means that separator is absent); the rest is
        // the comparison tail.
        var dash = javaVersion.IndexOf('-');
        var plus = javaVersion.IndexOf('+');
        var separator = dash < 0 || plus < 0 ? Math.Max(dash, plus) : Math.Min(dash, plus);
        var head = (separator < 0 ? javaVersion : javaVersion[..separator]).Replace('_', '.');
        var suffix = separator < 0 ? string.Empty : javaVersion[separator..];

        // System.Version requires at least two segments; pad "27" to "27.0".
        if (!head.Contains('.'))
            head += ".0";

        if (!Version.TryParse(head, out var parsed))
            return false;

        // Missing components are zero: System.Version's -1 for an absent build or
        // revision would otherwise make 17.0.2 and 17.0.2.0 different versions.
        var core = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        var isPreRelease = dash >= 0 && (plus < 0 || dash < plus) && !IsLegacyBuildMarker(javaVersion, dash, plus);

        version = new JvmVersion(core, isPreRelease, suffix, unparsed: false, original: javaVersion);
        return true;
    }

    /// <summary>
    ///     Parses a raw <c>JAVA_VERSION</c> value. Unparseable input degrades to an
    ///     <see cref="Unknown" /> placeholder, so a JVM is never lost over its version
    ///     string.
    /// </summary>
    public static JvmVersion Parse(string? javaVersion) =>
        TryParse(javaVersion, out var version) ? version : Unknown(javaVersion ?? string.Empty);

    /// <summary>A placeholder for unparseable values; it sorts before every parsed
    /// version and compares naturally against other placeholders by its raw string.</summary>
    public static JvmVersion Unknown(string original = "") => new(Zero, false, original, unparsed: true, original);

    /// <summary>Compares by numeric core, then by release stage, then by the build
    /// number and optional tag in natural order; unparseable values sort before every
    /// parsed version and naturally among themselves.</summary>
    public int CompareTo(JvmVersion other)
    {
        if (IsUnparsed || other.IsUnparsed)
        {
            if (IsUnparsed != other.IsUnparsed)
                return IsUnparsed ? -1 : 1;

            return NaturalCompare(Suffix, other.Suffix);
        }

        var core = Core.CompareTo(other.Core);
        if (core != 0)
            return core;

        // Same number: the release outranks its pre-release builds.
        var stage = other.IsPreRelease.CompareTo(IsPreRelease);
        if (stage != 0)
            return stage;

        // Same number and stage: the build number and optional tag decide, so +10
        // outranks +9 and the legacy -b10 outranks -b6.
        return NaturalCompare(Suffix, other.Suffix);
    }

    /// <summary>Indicates whether the left version orders strictly before the right.</summary>
    public static bool operator <(JvmVersion left, JvmVersion right) => left.CompareTo(right) < 0;

    /// <summary>Indicates whether the left version orders strictly after the right.</summary>
    public static bool operator >(JvmVersion left, JvmVersion right) => left.CompareTo(right) > 0;

    /// <summary>Indicates whether the left version orders before or equal to the right.</summary>
    public static bool operator <=(JvmVersion left, JvmVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Indicates whether the left version orders after or equal to the right.</summary>
    public static bool operator >=(JvmVersion left, JvmVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Returns the original version string.</summary>
    public override string ToString() => Original;

    /// <summary>Indicates whether two values describe the same version: same numeric
    /// core, same release stage and the same build/tag tail — so <c>17.0.2</c> equals
    /// <c>17.0.2.0</c> and <c>21.0.5+9-LTS</c> equals <c>21.0.5+9-lts</c>. Unparseable
    /// values are equal when their raw strings match.</summary>
    public bool Equals(JvmVersion other) =>
        IsUnparsed == other.IsUnparsed &&
        IsPreRelease == other.IsPreRelease &&
        Core.Equals(other.Core) &&
        string.Equals(Suffix, other.Suffix, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Core, IsPreRelease, Suffix, IsUnparsed);

    /// <summary>JDK 8 writes its build number after the dash in the legacy
    /// <c>1.8.0_402-b10</c> notation; that is a release build, not a pre-release tag.
    /// Modern versions follow JEP 223, where a dash always opens a pre-release.</summary>
    private static bool IsLegacyBuildMarker(string value, int dash, int plus)
    {
        if (!value.AsSpan(0, dash).Contains('_'))
            return false;

        var start = dash + 1;
        var end = plus < 0 ? value.Length : plus;
        if (end - start < 2 || value[start] is not ('b' or 'B'))
            return false;

        for (var index = start + 1; index < end; index++)
        {
            if (!char.IsAsciiDigit(value[index]))
                return false;
        }

        return true;
    }

    /// <summary>Canonicalizes a comparison tail — lowercased, digit runs stripped of
    /// leading zeros — so ordinal equality and natural ordering agree.</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            if (char.IsAsciiDigit(value[index]))
            {
                var end = index;
                while (end < value.Length && char.IsAsciiDigit(value[end]))
                    end++;

                var start = index;
                while (start < end - 1 && value[start] == '0')
                    start++;

                for (var digit = start; digit < end; digit++)
                    builder.Append(value[digit]);

                index = end;
            }
            else
            {
                builder.Append(char.ToLowerInvariant(value[index]));
                index++;
            }
        }

        return builder.ToString();
    }

    /// <summary>Compares canonicalized tails naturally: digit runs compare as numbers
    /// (by digit count, then digit by digit), everything else by code unit.</summary>
    private static int NaturalCompare(string left, string right)
    {
        int leftIndex = 0, rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (char.IsAsciiDigit(left[leftIndex]) && char.IsAsciiDigit(right[rightIndex]))
            {
                var leftEnd = leftIndex;
                while (leftEnd < left.Length && char.IsAsciiDigit(left[leftEnd]))
                    leftEnd++;
                var rightEnd = rightIndex;
                while (rightEnd < right.Length && char.IsAsciiDigit(right[rightEnd]))
                    rightEnd++;

                var digits = (leftEnd - leftIndex).CompareTo(rightEnd - rightIndex);
                if (digits != 0)
                    return digits;

                digits = string.CompareOrdinal(left, leftIndex, right, rightIndex, leftEnd - leftIndex);
                if (digits != 0)
                    return digits;

                leftIndex = leftEnd;
                rightIndex = rightEnd;
            }
            else
            {
                var difference = left[leftIndex].CompareTo(right[rightIndex]);
                if (difference != 0)
                    return difference;

                leftIndex++;
                rightIndex++;
            }
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }
}
